namespace NiyazisugarItemci;

internal static class BrowserTabRules
{
    internal static string Family(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("itemci.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".itemci.com", StringComparison.OrdinalIgnoreCase))) return "";
        var path = uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        if (path.StartsWith("/panel/mesaj") || path.StartsWith("/panel/message") || path.StartsWith("/panel/chat")) return "messages";
        if (path is "/panel/ilan-ekle" or "/panel/ilan-olustur" or "/panel/ilan-ver" || path.StartsWith("/panel/ilan-ekle/") || path.StartsWith("/panel/ilan-olustur/")) return "create";
        return "";
    }

    internal static string Identity(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(x => x, StringComparer.Ordinal);
        var normalizedQuery = string.Join("&", query);
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant() + uri.AbsolutePath.TrimEnd('/') +
            (normalizedQuery.Length > 0 ? "?" + normalizedQuery : "");
    }

    internal static bool SamePage(string first, string second) => Identity(first) == Identity(second);
    internal static bool IsMessageLanding(string value)
    {
        if (Family(value) != "messages" || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Query)) return false;
        var end = uri.AbsolutePath.TrimEnd('/').Split('/').Last();
        return end is "mesajlar" or "mesaj-kutusu" or "messages" or "chat" or "mesaj";
    }
}
