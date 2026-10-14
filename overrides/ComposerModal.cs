namespace NiyazisugarItemci;

internal static class ComposerModal
{
    internal static Task ShowAsync(Form owner,Form popup)
    {
        var closed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Never enter a modal loop from a WebView2 callback or the inline
        // continuation of ExecuteScriptAsync. Let that callback unwind first.
        owner.BeginInvoke(new Action(()=>
        {
            try
            {
                if(owner.IsDisposed||popup.IsDisposed){closed.TrySetResult(false);return;}
                popup.ShowDialog(owner);
                closed.TrySetResult(true);
            }
            catch(Exception ex){closed.TrySetException(ex);}
        }));
        return closed.Task;
    }
}
