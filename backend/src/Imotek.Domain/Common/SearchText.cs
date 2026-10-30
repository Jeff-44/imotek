using System.Globalization;
using System.Text;

namespace Imotek.Domain.Common;

/// <summary>
/// Builds accent- and case-insensitive search keys ("Pétion-Ville" → "petion-ville").
/// Stored keys and search terms go through the same function, so matching behaves the
/// same in memory and in SQL, without database extensions.
/// </summary>
public static class SearchText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            switch (c)
            {
                case '’' or '‘' or 'ʼ':
                    builder.Append('\'');
                    break;
                case 'œ' or 'Œ':
                    builder.Append("oe");
                    break;
                default:
                    builder.Append(char.ToLowerInvariant(c));
                    break;
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string Join(params string?[] values) =>
        string.Join(' ', values.Select(Normalize).Where(v => v.Length > 0));
}
