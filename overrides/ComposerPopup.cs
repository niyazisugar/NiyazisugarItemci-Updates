using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Form? _composerPopup;
    private bool _composerPopupOpening;
    private Button? _composerCopyLink;
    private DateTime _composerCooldownUntilUtc;
    private string _composerLastLink="";
    private readonly System.Windows.Forms.Timer _composerCooldownTimer=new(){Interval=1000};
    private string ComposerImagesFolder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","ListingImages");
    private string ComposerPopupStatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","composer-state.json");

    private void InstallComposerPopupExtras(FlowLayoutPanel actions,FlowLayoutPanel pictures)
    {
        actions.WrapContents=true;actions.Height=96;
        _composerCopyLink=ActionButton("İLAN LİNKİNİ KOPYALA",()=>{if(_composerLastLink.Length>0){Clipboard.SetText(_composerLastLink);_composerStatus.Text="İlan linki kopyalandı.";}});
        _composerCopyLink.Width=190;actions.Controls.Add(_composerCopyLink);
        var saved=ActionButton("HAZIR GÖRSELLER",ShowComposerImageLibrary);saved.Width=170;pictures.Controls.Add(saved);
        foreach(var combo in new[]{_composerServer,_composerDelivery,_composerValidity}){combo.BackColor=Card;combo.ForeColor=TextMain;combo.FlatStyle=FlatStyle.Flat;combo.Font=new Font("Segoe UI",10);}
        _composerImages.BackColor=Card;_composerImages.ForeColor=TextMain;_composerImages.BorderStyle=BorderStyle.FixedSingle;
        _composerPrice.BackColor=Card;_composerPrice.ForeColor=TextMain;_composerPrice.Font=new Font("Segoe UI",10);
        try{if(File.Exists(ComposerPopupStatePath)){var state=JsonSerializer.Deserialize<ComposerPopupState>(File.ReadAllText(ComposerPopupStatePath));if(state is not null){_composerCooldownUntilUtc=state.untilUtc;if(IsComposerListingLink(state.link))_composerLastLink=state.link;}}}catch{}
        _composerCooldownTimer.Tick+=(_,_)=>UpdateComposerPublishAvailability();_composerCooldownTimer.Start();
        FormClosed+=(_,_)=>_composerCooldownTimer.Dispose();UpdateComposerPublishAvailability();
    }
    private async Task ShowListingComposerPopupAsync()
    {
        if(_composerPopup is not null){_composerPopup.Activate();return;}
        if(_composerPopupOpening)return;_composerPopupOpening=true;
        try
        {
            if(_marketWebReady)await RefreshMarketServersAsync();
            RefreshComposerServerChoices();
            var area=Screen.FromControl(this).WorkingArea;
            using var popup=new Form{Text="Hızlı İlan Ekle",Size=new Size(Math.Min(900,area.Width-60),Math.Min(900,area.Height-60)),MinimumSize=new Size(720,560),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=TextMain,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false,Padding=new Padding(6)};
            _composerPopup=popup;
            _content.Controls.Remove(_composerPanel);popup.Controls.Add(_composerPanel);_composerPanel.Visible=true;
            popup.FormClosing+=(_,e)=>{if(_composerBusy){e.Cancel=true;_composerStatus.Text="Gönderim devam ediyor. Sonuç geldikten sonra pencereyi kapatabilirsin.";}};
            PremiumTheme.Apply(popup);UpdateComposerPublishAvailability();
            try{popup.ShowDialog(this);}
            finally{SaveComposerDrafts();popup.Controls.Remove(_composerPanel);_composerPanel.Visible=false;_content.Controls.Add(_composerPanel);_composerPopup=null;}
        }
        finally{_composerPopupOpening=false;}
    }
    private void RefreshComposerServerChoices()
    {
            var id=(_composerServer.SelectedItem as MarketServerInfo)?.Id??_settings.LastQuickListingServer;
            _composerLoading=true;_composerServer.Items.Clear();foreach(var server in _marketServers)_composerServer.Items.Add(server);_composerServer.DisplayMember="Name";_composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Id==id)??_marketServers.FirstOrDefault();_composerLoading=false;LoadComposerServerDraft();
    }
    private string SaveComposerLibraryImage(string path)
    {
        if(!File.Exists(path))throw new IOException("Görsel dosyası bulunamadı.");
        var file=new FileInfo(path);var extension=file.Extension.ToLowerInvariant();
        if(file.Length>5*1024*1024||extension is not (".png" or ".jpg" or ".jpeg"))throw new IOException("PNG/JPG ve en fazla 5 MB görsel seçmelisin.");
        Directory.CreateDirectory(ComposerImagesFolder);
        if(string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),Path.GetFullPath(ComposerImagesFolder),StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(path);
        using var stream=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(stream))[..16];
        var name=Regex.Replace(Path.GetFileNameWithoutExtension(path),@"[^\p{L}\p{N} _-]","");if(name.Length>60)name=name[..60];
        var destination=Path.Combine(ComposerImagesFolder,name+"-"+hash+extension);
        if(!File.Exists(destination))File.Copy(path,destination);
        return destination;
    }
    private void ShowComposerImageLibrary()
    {
        Directory.CreateDirectory(ComposerImagesFolder);
        using var dialog=new Form{Text="Hazır Görseller",Size=new Size(690,490),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=TextMain,Padding=new Padding(18),MinimizeBox=false,MaximizeBox=false};
        var gallery=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true};
        var note=new Label{Dock=DockStyle.Top,Height=42,Text="Bir görsele tıklayarak ilana ekle. En fazla 4 görsel seçebilirsin.",ForeColor=TextMuted};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,WrapContents=false};
        foreach(var path in Directory.GetFiles(ComposerImagesFolder).Where(p=>new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(p).ToLowerInvariant())))
        {
            var card=new Panel{Width=145,Height=132,BackColor=Card,Margin=new Padding(0,0,10,10),Cursor=Cursors.Hand};
            var preview=new PictureBox{Dock=DockStyle.Top,Height=95,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Card};
            try{using var stream=File.OpenRead(path);using var image=Image.FromStream(stream);preview.Image=new Bitmap(image);}catch{card.Dispose();continue;}
            card.Disposed+=(_,_)=>preview.Image?.Dispose();
            var title=new Label{Dock=DockStyle.Fill,Text=Path.GetFileNameWithoutExtension(path).Split('-')[0],AutoEllipsis=true,TextAlign=ContentAlignment.MiddleCenter,ForeColor=TextMain};
            void Select(){if(!_composerImages.Items.Contains(path)){if(_composerImages.Items.Count>=4){note.Text="En fazla 4 görsel. Önce seçilenlerden birini kaldır.";return;}_composerImages.Items.Add(path);}_composerStock.Checked=false;QueueComposerSave();note.Text=$"İlana eklenen: {_composerImages.Items.Count}/4 · Seçimler kaydediliyor.";card.BackColor=Color.FromArgb(49,60,44);}
            foreach(Control control in new Control[]{card,preview,title})control.Click+=(_,_)=>Select();
            card.Controls.Add(title);card.Controls.Add(preview);title.BringToFront();gallery.Controls.Add(card);
        }
        if(gallery.Controls.Count==0)gallery.Controls.Add(new Label{Text="Henüz hazır görsel yok. Görsel Ekle ile seçtiğin dosyalar burada saklanır.",AutoSize=true,ForeColor=TextMuted});
        footer.Controls.Add(ActionButton("GÖRSEL EKLE",()=>{SelectComposerPictures();note.Text="Görseller kaydedildi. Galeriyi tekrar açarak görebilirsin.";}));
        footer.Controls.Add(ActionButton("TAMAM",()=>dialog.Close()));
        dialog.Controls.Add(gallery);dialog.Controls.Add(note);dialog.Controls.Add(footer);gallery.BringToFront();PremiumTheme.Apply(dialog);dialog.ShowDialog(_composerPopup??this);
    }
    private static bool IsComposerListingLink(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="itemci.com"&&uri.AbsolutePath.StartsWith("/product/");
    private void SaveComposerPopupState(){try{Directory.CreateDirectory(Path.GetDirectoryName(ComposerPopupStatePath)!);File.WriteAllText(ComposerPopupStatePath,JsonSerializer.Serialize(new ComposerPopupState{untilUtc=_composerCooldownUntilUtc,link=_composerLastLink}));}catch{}}
    private void UpdateComposerPublishAvailability()
    {
        if(_composerPublish is null)return;
        var remaining=Math.Max(0,(int)Math.Ceiling((_composerCooldownUntilUtc-DateTime.UtcNow).TotalSeconds));
        _composerPublish.Enabled=!_composerBusy&&!_composerUncertain&&remaining==0;
        _composerPublish.Text=remaining>0?$"YAYINLA ({remaining} sn)":"İLANI YAYINLA";
        if(_composerCopyLink is not null)_composerCopyLink.Enabled=IsComposerListingLink(_composerLastLink);
    }
    private void ApplyComposerServerWait(ComposerResult result)
    {
        int seconds=result.retryAfterSeconds;
        var match=Regex.Match(result.message,@"(?<n>\d+)\s*(?<unit>saniye|dakika)",RegexOptions.IgnoreCase);
        if(seconds<=0&&match.Success&&int.TryParse(match.Groups["n"].Value,out var n))seconds=n*(match.Groups["unit"].Value.StartsWith("dakika",StringComparison.OrdinalIgnoreCase)?60:1);
        if(seconds>0){_composerCooldownUntilUtc=DateTime.UtcNow.AddSeconds(seconds);SaveComposerPopupState();}
    }
    private sealed class ComposerPopupState{public DateTime untilUtc{get;set;}public string link{get;set;}="";}
}
