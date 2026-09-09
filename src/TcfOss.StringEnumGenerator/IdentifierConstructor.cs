using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace TcfOss.StringEnumGenerator;

public static partial class IdentifierConstructor
{
    private static readonly Regex s_nonIdentifierChars = CreateNonIdentifierCharacterRegex();
    private static readonly HashSet<string> s_usedVariableNames = ["Equals", "GetHashCode", "ToString", "FromText", "TryParse", "Text", "AllowedValues", "_text", "s_allowedValues"];

    public static string ToVariableName(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Value";
        }

        string[] parts = [.. s_nonIdentifierChars.Split(value).Where(p => !string.IsNullOrEmpty(p))];
        if (parts.Length == 0)
        {
            parts = [value];
        }

        var sb = new StringBuilder();
        foreach (string p in parts)
        {
            string lower = p.ToLowerInvariant();
            char first = char.ToUpperInvariant(lower[0]);
            if (lower.Length > 1)
            {
                sb.Append(first).Append(lower.AsSpan(1));
            }
            else
            {
                sb.Append(first);
            }
        }

        string ident = sb.ToString();
        if (char.IsDigit(ident[0]))
        {
            ident = "_" + ident;
        }

        return ident;
    }

    public static IdentifierError IsValidCSharpIdentifier(this string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return IdentifierError.Empty;
        }

        // Basic form validation (characters, etc.)
        if (!SyntaxFacts.IsValidIdentifier(identifier))
        {
            return IdentifierError.Invalid;
        }

        // Reject reserved keywords (e.g. `string`, `class`, `int`)
        if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None)
        {
            return IdentifierError.ReservedKeyword;
        }

        // Reject contextual keywords (e.g. `var` in some contexts)
        if (SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None)
        {
            return IdentifierError.ReservedKeyword;
        }

        if (s_usedVariableNames.Contains(identifier))
        {
            return IdentifierError.UsedVariableName;
        }

        return IdentifierError.None;
    }


    [GeneratedRegex(@"[^0-9A-Za-z]+")]
    private static partial Regex CreateNonIdentifierCharacterRegex();
}
