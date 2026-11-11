namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private void OnFormClosing(object? sender,FormClosingEventArgs e)
    {
        if(_allowExit)return;
        // A Windows shutdown must close the process, not start a new tray transition.
        if(e.CloseReason!=CloseReason.UserClosing){_allowExit=true;return;}
        e.Cancel=true;
        HideToTray(showInfo:true);
    }
    private void HideToTray(bool showInfo)
    {
        if(IsDisposed||Disposing||_allowExit)return;
        // Hide already removes the taskbar button. Changing ShowInTaskbar here
        // recreates the form handle and destroys initialized WebView2 children.
        Hide();
        if(showInfo)_trayIcon.ShowBalloonTip(2500,"NiyaziSUGAR · Itemci","Uygulama arka planda çalışmaya devam ediyor.",ToolTipIcon.Info);
    }
    private void RestoreFromTray()
    {
        if(IsDisposed||Disposing||_allowExit)return;
        Show();
        if(WindowState!=FormWindowState.Normal)WindowState=FormWindowState.Normal;
        Activate();
    }
}
