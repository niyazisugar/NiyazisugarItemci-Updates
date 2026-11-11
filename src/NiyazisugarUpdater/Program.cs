using System.Diagnostics;
using System.IO.Compression;

namespace NiyazisugarUpdater;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NiyazisugarItemci",
            "update.log"
        );

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);

            var map = ParseArgs(args);

            var pid = int.Parse(Require(map, "pid"));
            var zipPath = Require(map, "zip");
            var installFolder = Require(map, "install");
            var mainExe = Require(map, "exe");

            Log(logFile, $"Update start. PID={pid}");
            Log(logFile, $"ZIP={zipPath}");
            Log(logFile, $"INSTALL={installFolder}");

            WaitForProcessExit(pid, logFile);

            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Update package not found.", zipPath);

            var staging = Path.Combine(
                Path.GetTempPath(),
                "NiyazisugarItemci-Update-" + Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(staging);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

                var payloadRoot = ResolvePayloadRoot(staging);
                Directory.CreateDirectory(installFolder);

                CopyDirectory(payloadRoot, installFolder);

                Log(logFile, "Files copied successfully.");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging))
                        Directory.Delete(staging, recursive: true);
                }
                catch { }
            }

            try
            {
                File.Delete(zipPath);
            }
            catch { }

            if (!File.Exists(mainExe))
                throw new FileNotFoundException("Main executable missing after update.", mainExe);

            Process.Start(new ProcessStartInfo
            {
                FileName = mainExe,
                WorkingDirectory = installFolder,
                UseShellExecute = true
            });

            Log(logFile, "Update completed and app restarted.");
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                Log(logFile, "ERROR: " + ex);
            }
            catch { }

            return 1;
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];

            if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                continue;

            map[key[2..]] = args[++i];
        }

        return map;
    }

    private static string Require(Dictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{key}");

        return value;
    }

    private static void WaitForProcessExit(int pid, string logFile)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            Log(logFile, "Waiting for main app to exit...");

            if (!process.WaitForExit(30000))
            {
                Log(logFile, "Main app did not exit in time. Killing it.");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
            // Process already closed.
        }
    }

    private static string ResolvePayloadRoot(string staging)
    {
        if (File.Exists(Path.Combine(staging, "NiyazisugarItemci.exe")))
            return staging;

        var directories = Directory.GetDirectories(staging);

        if (directories.Length == 1 &&
            File.Exists(Path.Combine(directories[0], "NiyazisugarItemci.exe")))
        {
            return directories[0];
        }

        throw new InvalidDataException("Update package does not contain NiyazisugarItemci.exe.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void Log(string path, string message)
    {
        File.AppendAllText(
            path,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}"
        );
    }
}
