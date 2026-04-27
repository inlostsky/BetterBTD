using System.Text.RegularExpressions;

namespace BetterBTD.Helpers;

internal static partial class RegexHelper
{
    [GeneratedRegex(@"[^0-9]+")]
    public static partial Regex ExcludeNumberRegex();

    [GeneratedRegex(@"^[0-9]+$")]
    public static partial Regex FullNumberRegex();
}
