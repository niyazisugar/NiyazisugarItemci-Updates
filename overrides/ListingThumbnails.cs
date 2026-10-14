using System.Net.Http;
namespace NiyazisugarItemci;
internal static class ListingThumbnails
{
    private static readonly HttpClient Client=new() { Timeout=TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim Gate=new(2,2);
    internal static async Task<Bitmap?> LoadAsync(string address, Func<bool> stillNeeded)
    {
        if(address.StartsWith('/')) address="https://itemci.com"+address;
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !(uri.Host=="itemci.com"||uri.Host.EndsWith(".itemci.com",StringComparison.OrdinalIgnoreCase)))return null;
        await Gate.WaitAsync();
        try {
            if(!stillNeeded())return null;
            using var response=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>4*1024*1024)return null;
            using var stream=await response.Content.ReadAsStreamAsync();using var buffer=new MemoryStream();var bytes=new byte[16384];
            int read;while((read=await stream.ReadAsync(bytes))>0){if(buffer.Length+read>4*1024*1024)return null;buffer.Write(bytes,0,read);}
            if(!stillNeeded())return null;buffer.Position=0;
            return await Task.Run(()=>{
                using var image=Image.FromStream(buffer);
                if(image.Width>4096||image.Height>4096)return null;
                var thumb=new Bitmap(100,76);using var g=Graphics.FromImage(thumb);g.Clear(PremiumTheme.Background);var scale=Math.Min(100f/image.Width,76f/image.Height);var w=image.Width*scale;var h=image.Height*scale;g.DrawImage(image,(100-w)/2,(76-h)/2,w,h);return thumb;
            });
        }catch{return null;}finally{Gate.Release();}
    }
}
