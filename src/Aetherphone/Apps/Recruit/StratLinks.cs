// Likely a backend function, definitely isn't something we want on the frontend that can be manipulated.
namespace Aetherphone.Apps.Recruit;

internal static class StratLinks
{
    private const string WtfdigHost = "wtfdig.info";
    private const string WtfdigSubdomainSuffix = ".wtfdig.info";

    public static bool TryNormalizeWtfdig(string raw, out string normalized)
    {
        normalized = string.Empty;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var candidate = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "https://" + trimmed;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        var host = uri.Host;
        var isWtfdig = host.Equals(WtfdigHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(WtfdigSubdomainSuffix, StringComparison.OrdinalIgnoreCase);
        if (!isWtfdig)
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }
}
