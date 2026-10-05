namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

/// <summary>Builds ILIKE patterns from user input, so <c>%</c> and <c>_</c> in a search term match literally.</summary>
internal static class LikePatterns
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string term) =>
        $"%{term.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter).Replace("%", EscapeCharacter + "%").Replace("_", EscapeCharacter + "_")}%";
}
