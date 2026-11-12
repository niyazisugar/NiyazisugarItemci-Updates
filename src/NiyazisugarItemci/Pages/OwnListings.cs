using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;

// İlanlarım: aktif ilanlar Itemci > İlanlarım (Satışta) sayfasından okunur.
// Bu sayfa tarayıcıda çizildiği için ham HTML ile değil, gizli WebView ile okunur.
public sealed partial class MainForm
{
    private sealed class OwnListingRow
    {
        public string title { get; set; } = "";
        public string url { get; set; } = "";
        public string price { get; set; } = "";
        public string server { get; set; } = "";
        public string image { get; set; } = "";
        public string number { get; set; } = "";
    }
    private sealed class OwnListingPage
    {
        public int? total { get; set; }
        public List<OwnListingRow> rows { get; set; } = new();
    }

    // null: panel okunamadı (eski yönteme geçilir). Boş liste: gerçekten satışta ilan yok.
    private async Task<List<ListingInfo>?> ReadPanelActiveListingsAsync()
    {
        if (_browserEnvironment is null || SiteAutomationBlocked) return null;
        // Satış taraması aynı gizli tarayıcıyı kullanır; kısa süre bitmesini bekle.
        for (var i = 0; i < 40 && _soldScanBusy; i++) await Task.Delay(250);
        if (_soldScanBusy) return null;
        _soldScanBusy = true;
        try
        {
            await EnsureSoldScanViewAsync();
            var core = _soldScanView!.CoreWebView2;
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OwnListings.js"));
            var result = new List<ListingInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var page = 1; page <= 10; page++)
            {
                await NavigateOwnListingsPageAsync(core, $"https://itemci.com/panel/ilanlarim?page={page}&status=1");
                await core.ExecuteScriptAsync(script);
                var data = await ReadOwnListingsPageAsync(core);
                var added = 0;
                foreach (var row in data.rows)
                {
                    if (string.IsNullOrWhiteSpace(row.url) || !seen.Add(row.url)) continue;
                    added++;
                    result.Add(new ListingInfo
                    {
                        Title = row.title,
                        Url = row.url,
                        PriceText = row.price,
                        Status = "AKTİF",
                        ImageUrl = row.image,
                        Server = string.IsNullOrWhiteSpace(row.server) ? "Bilinmeyen sunucu" : row.server
                    });
                }
                if (added == 0 || data.total is null || result.Count >= data.total) break;
            }
            return result;
        }
        catch
        {
            return null;
        }
        finally { _soldScanBusy = false; }
    }

    private static async Task NavigateOwnListingsPageAsync(CoreWebView2 core, string url)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess) completion.TrySetResult(true);
            else completion.TrySetException(new IOException("İlanlarım sayfası yüklenemedi: " + args.WebErrorStatus));
        }
        core.NavigationCompleted += Completed;
        try
        {
            core.Navigate(url);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) || uri.Host != "itemci.com" || !uri.AbsolutePath.Equals("/panel/ilanlarim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Itemci hesabına giriş yapılmamış.");
        }
        finally { core.NavigationCompleted -= Completed; }
    }

    private static async Task<OwnListingPage> ReadOwnListingsPageAsync(CoreWebView2 core)
    {
        var token = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<OwnListingPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Received(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                using var json = JsonDocument.Parse(args.WebMessageAsJson);
                var root = json.RootElement;
                if (!root.TryGetProperty("ownListingsToken", out var id) || id.GetString() != token) return;
                if (root.TryGetProperty("error", out var error)) { completion.TrySetException(new InvalidOperationException(error.GetString())); return; }
                completion.TrySetResult(root.GetProperty("result").Deserialize<OwnListingPage>() ?? new OwnListingPage());
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        }
        core.WebMessageReceived += Received;
        try
        {
            await core.ExecuteScriptAsync($"window.__niyaziReadOwnActiveListings().then(result => window.chrome.webview.postMessage({{ownListingsToken:'{token}',result}})).catch(e => window.chrome.webview.postMessage({{ownListingsToken:'{token}',error:String(e.message || e)}}));");
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
        }
        finally { core.WebMessageReceived -= Received; }
    }
}
