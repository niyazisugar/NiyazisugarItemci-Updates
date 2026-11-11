using System.Net.Http;
namespace NiyazisugarItemci;
internal static class ListingThumbnails
{
    private static readonly HttpClient Client=new() { Timeout=TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim Gate=new(2,2);
    private static readonly Dictionary<string,Bitmap> Cache=new();
    private static readonly Queue<string> Order=new();
    private static readonly object CacheLock=new();
    private static Bitmap? Cached(string key) {lock(CacheLock)return Cache.TryGetValue(key,out var image)?new Bitmap(image):null;}
    private static Bitmap Remember(string key,Bitmap image) {
        lock(CacheLock) {
            if(Cache.TryGetValue(key,out var old)) {image.Dispose();return new Bitmap(old);}
            while(Cache.Count>=32) {var victim=Order.Dequeue();Cache[victim].Dispose();Cache.Remove(victim);}
            Cache[key]=image;Order.Enqueue(key);return new Bitmap(image);
        }
    }
    internal static async Task<Bitmap?> LoadAsync(string address, Func<bool> stillNeeded)
    {
        if(address.StartsWith("//"))address="https:"+address;
        else if(address.StartsWith('/')) address="https://itemci.com"+address;
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !(uri.Host=="itemci.com"||uri.Host.EndsWith(".itemci.com",StringComparison.OrdinalIgnoreCase)))return null;
        if(!stillNeeded())return null;var cached=Cached(uri.AbsoluteUri);if(cached is not null)return cached;
        await Gate.WaitAsync();
        try {
            if(!stillNeeded())return null;cached=Cached(uri.AbsoluteUri);if(cached is not null)return cached;
            using var response=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>4*1024*1024)return null;
            using var stream=await response.Content.ReadAsStreamAsync();using var buffer=new MemoryStream();var bytes=new byte[16384];
            int read;while((read=await stream.ReadAsync(bytes))>0){if(buffer.Length+read>4*1024*1024)return null;buffer.Write(bytes,0,read);}
            if(!stillNeeded())return null;buffer.Position=0;
            var thumbnail=await Task.Run(()=>{
                using var image=Image.FromStream(buffer);
                if(image.Width>4096||image.Height>4096)return null;
                var thumb=new Bitmap(100,76);using var g=Graphics.FromImage(thumb);g.Clear(PremiumTheme.Background);var scale=Math.Min(100f/image.Width,76f/image.Height);var w=image.Width*scale;var h=image.Height*scale;g.DrawImage(image,(100-w)/2,(76-h)/2,w,h);return thumb;
            });
            return thumbnail is null ? null : Remember(uri.AbsoluteUri,thumbnail);
        }catch{return null;}finally{Gate.Release();}
    }
}
