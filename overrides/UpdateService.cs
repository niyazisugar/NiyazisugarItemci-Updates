using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NiyazisugarItemci;

internal sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = "";
}

internal static class UpdateService
{
    public const string CurrentVersion = "2.7.1";

    public const string FeedUrl =
        "https://github.com/niyazisugar/NiyazisugarItemci-Updates/releases/latest/download/latest.json";

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"NiyazisugarItemci/{CurrentVersion}"
        );

        client.DefaultRequestHeaders.CacheControl =
            new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

        return client;
    }

    public static async Task<UpdateManifest?> GetLatestAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.CacheControl =
            new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        if (manifest is null ||
            string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            throw new InvalidDataException("Güncelleme bilgisi eksik veya bozuk.");
        }

        return manifest;
    }

    public static bool IsNewerVersion(string? remoteVersion)
    {
        if (!TryParseVersion(remoteVersion, out var remote))
            return false;

        if (!TryParseVersion(CurrentVersion, out var current))
            return false;

        return remote > current;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];

        if (!Version.TryParse(value, out var parsed) || parsed is null)
            return false;

        version = parsed;
        return true;
    }

    public static async Task<string> DownloadPackageAsync(
        UpdateManifest manifest,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var updatesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NiyazisugarItemci",
            "Updates"
        );

        Directory.CreateDirectory(updatesFolder);

        var safeVersion = RegexSafeVersion(manifest.Version);
        var finalPath = Path.Combine(
            updatesFolder,
            $"NiyazisugarItemci-{safeVersion}.zip"
        );

        var tempPath = Path.Combine(
            updatesFolder,
            $"NiyazisugarItemci-{safeVersion}-{Guid.NewGuid():N}.download"
        );

        using var response = await Client.GetAsync(
            manifest.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 128,
            useAsync: true
        ))
        {
            var buffer = new byte[1024 * 128];
            long readTotal = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read <= 0)
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readTotal += read;

                if (total is > 0)
                {
                    var percent = (int)Math.Clamp(
                        readTotal * 100L / total.Value,
                        0,
                        100
                    );

                    progress?.Report(percent);
                }
            }

            await output.FlushAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actualHash = await ComputeSha256WithRetryAsync(
                tempPath,
                cancellationToken
            );
            var expectedHash = manifest.Sha256.Trim().Replace(" ", "");

            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteQuietly(tempPath);
                throw new InvalidDataException(
                    "İndirilen güncellemenin güvenlik doğrulaması başarısız oldu."
                );
            }
        }

        await MoveDownloadedFileWithRetryAsync(
            tempPath,
            finalPath,
            cancellationToken
        );

        progress?.Report(100);
        return finalPath;
    }

    private static async Task<string> ComputeSha256WithRetryAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 12; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    1024 * 128,
                    useAsync: true
                );

                using var sha = SHA256.Create();
                var hash = await sha.ComputeHashAsync(stream, cancellationToken);
                return Convert.ToHexString(hash);
            }
            catch (IOException ex)
            {
                lastError = ex;
                if (attempt == 12)
                    break;

                await Task.Delay(250, cancellationToken);
            }
        }

        throw new IOException(
            "Güncelleme dosyası doğrulama için açılamadı. Dosya başka bir işlem tarafından geçici olarak kullanılıyor olabilir.",
            lastError
        );
    }

    private static async Task MoveDownloadedFileWithRetryAsync(
        string tempPath,
        string finalPath,
        CancellationToken cancellationToken
    )
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 12; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(finalPath))
                    File.Delete(finalPath);

                File.Move(tempPath, finalPath);
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
                if (attempt == 12)
                    break;

                await Task.Delay(250, cancellationToken);
            }
        }

        throw new IOException(
            "Güncelleme dosyası hazırlanamadı. Dosya başka bir işlem tarafından geçici olarak kullanılıyor olabilir.",
            lastError
        );
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static string RegexSafeVersion(string version)
    {
        var chars = version
            .Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
            .ToArray();

        var value = new string(chars);
        return string.IsNullOrWhiteSpace(value) ? "update" : value;
    }

    public static void StartUpdaterAndExit(
        string packagePath,
        int currentProcessId
    )
    {
        var sourceUpdater = Path.Combine(
            AppContext.BaseDirectory,
            "NiyazisugarUpdater.exe"
        );

        if (!File.Exists(sourceUpdater))
        {
            throw new FileNotFoundException(
                "Güncelleme yardımcısı bulunamadı.",
                sourceUpdater
            );
        }

        var tempUpdaterFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NiyazisugarItemci",
            "Updater"
        );

        Directory.CreateDirectory(tempUpdaterFolder);

        var tempUpdaterPath = Path.Combine(
            tempUpdaterFolder,
            $"NiyazisugarUpdater-{DateTime.Now:yyyyMMddHHmmss}.exe"
        );

        File.Copy(sourceUpdater, tempUpdaterPath, overwrite: true);

        var installFolder = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        var mainExe = Path.Combine(
            installFolder,
            "NiyazisugarItemci.exe"
        );

        var psi = new ProcessStartInfo
        {
            FileName = tempUpdaterPath,
            UseShellExecute = true,
            WorkingDirectory = tempUpdaterFolder
        };

        psi.ArgumentList.Add("--pid");
        psi.ArgumentList.Add(currentProcessId.ToString());
        psi.ArgumentList.Add("--zip");
        psi.ArgumentList.Add(packagePath);
        psi.ArgumentList.Add("--install");
        psi.ArgumentList.Add(installFolder);
        psi.ArgumentList.Add("--exe");
        psi.ArgumentList.Add(mainExe);

        Process.Start(psi);
    }
}
