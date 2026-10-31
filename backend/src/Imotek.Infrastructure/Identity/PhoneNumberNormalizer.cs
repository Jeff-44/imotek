using Imotek.Application.Common;
using PhoneNumbers;

namespace Imotek.Infrastructure.Identity;

/// <summary>
/// Validates numbers against each country's numbering plan (libphonenumber) and returns them in
/// E.164 form. Numbers written without "+" are read as Haitian. No SMS is sent: this checks the
/// format, not that the number belongs to the user.
/// </summary>
public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private const int MaxInputLength = 32;
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public NormalizedPhoneNumber? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxInputLength)
        {
            return null;
        }

        var text = input.Trim();

        // libphonenumber turns letters into keypad digits ("abcd" → "2223"); a typo must not
        // silently become someone else's number, so only digits and separators are accepted.
        if (!text.All(c => char.IsAsciiDigit(c) || c is ' ' or '+' or '-' or '.' or '(' or ')'))
        {
            return null;
        }

        if (text.StartsWith("00", StringComparison.Ordinal))
        {
            text = "+" + text[2..]; // international dialing prefix used in Haiti and Europe
        }

        try
        {
            var number = Util.Parse(text, IPhoneNumberNormalizer.DefaultRegion);
            if (!Util.IsValidNumber(number))
            {
                return null;
            }

            return new NormalizedPhoneNumber(
                Util.Format(number, PhoneNumberFormat.E164),
                Util.GetRegionCodeForNumber(number) ?? string.Empty);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
