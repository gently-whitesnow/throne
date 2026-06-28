namespace Throne.Application.Search;

/// <summary>
/// Sentinel markers wrapping matched terms inside <see cref="IntentSearchHit.Snippet"/>.
/// Control characters (STX / ETX) are used on purpose: they never occur in intent text, so
/// clients can split on them and wrap the enclosed run as a highlight without risking HTML
/// injection (the snippet is rendered as text nodes, not raw markup).
/// </summary>
public static class IntentSearchMarkers
{
    public const string Open = "\u0002";
    public const string Close = "\u0003";
}
