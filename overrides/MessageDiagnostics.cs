using System.Text.Json;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private async Task SaveMessageLayoutAsync()
    {
        var tab=_browserTabs.FirstOrDefault(t=>BrowserTabRules.Family(TabLocation(t))=="messages");
        if(tab?.WebView.CoreWebView2 is null){MessageBox.Show("Önce Mesajlar bölümünü açıp bir konuşma seç, ardından Mesajlar menüsüne sağ tıklayarak kaydı al.","Mesaj bağlantısı");return;}
        try
        {
            await InstallQuickReplyDockAsync(tab.WebView);
            var raw=await tab.WebView.CoreWebView2.ExecuteScriptAsync("location.pathname.startsWith('/panel/mesajlar') && window.__niyaziMessageLayout ? window.__niyaziMessageLayout() : null");
            using var document=JsonDocument.Parse(raw);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Mesaj sayfası hazır değil. Bir konuşma açıp yeniden dene.");
            using var dialog=new SaveFileDialog {Filter="Teknik kayıt (*.json)|*.json",FileName="itemci-message-layout.json",Title="Mesaj bağlantısı kaydı"};
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            await File.WriteAllTextAsync(dialog.FileName,JsonSerializer.Serialize(document.RootElement,new JsonSerializerOptions {WriteIndented=true}));
            MessageBox.Show("Teknik kayıt kaydedildi. Bu dosya mesaj metinlerini veya oturum bilgilerini içermez.","Mesaj bağlantısı");
        }
        catch(Exception ex){MessageBox.Show("Kayıt alınamadı: "+ex.Message,"Mesaj bağlantısı");}
    }
}
