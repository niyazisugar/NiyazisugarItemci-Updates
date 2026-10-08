namespace NiyazisugarItemci;

internal static class AdBlockRules
{
    private static readonly string[] BlockedHosts =
    {
        "doubleclick.net",
        "googlesyndication.com",
        "googletagservices.com",
        "googleadservices.com",
        "adservice.google.com",
        "adnxs.com",
        "appnexus.com",
        "criteo.com",
        "criteo.net",
        "taboola.com",
        "outbrain.com",
        "adform.net",
        "pubmatic.com",
        "rubiconproject.com",
        "openx.net",
        "adsrvr.org",
        "casalemedia.com",
        "smartadserver.com",
        "amazon-adsystem.com",
        "scorecardresearch.com",
        "moatads.com",
        "mgid.com",
        "revcontent.com",
        "adskeeper.co.uk",
        "exoclick.com",
        "popads.net"
    };

    private static readonly string[] BlockedPathFragments =
    {
        "/pagead/",
        "/gampad/",
        "/adserver/",
        "/googleads/",
        "/adsystem/"
    };

    public static bool ShouldBlock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        var host = uri.Host.ToLowerInvariant();

        // Itemci'nin kendi kaynaklarini network seviyesinde engelleme.
        // Site fonksiyonlarini bozmadan ilk-parti bannerlar DOM filtresiyle gizlenir.
        if (
            host.Equals("itemci.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".itemci.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        foreach (var blockedHost in BlockedHosts)
        {
            if (
                host.Equals(blockedHost, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + blockedHost, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        var pathAndQuery =
            (uri.AbsolutePath + uri.Query).ToLowerInvariant();

        return BlockedPathFragments.Any(
            fragment => pathAndQuery.Contains(fragment, StringComparison.OrdinalIgnoreCase)
        );
    }
}
