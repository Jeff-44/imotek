using Imotek.Infrastructure.Identity;

namespace Imotek.Tests.Infrastructure;

public class PhoneNumberNormalizerTests
{
    private readonly PhoneNumberNormalizer _normalizer = new();

    [Theory]
    // Haiti (+509), in the ways people actually write it
    [InlineData("+509 3712 3456", "+50937123456", "HT")]
    [InlineData("+509-37-12-34-56", "+50937123456", "HT")]
    [InlineData("50937123456", "+50937123456", "HT")]
    [InlineData("3712 3456", "+50937123456", "HT")]
    [InlineData("0050937123456", "+50937123456", "HT")]
    [InlineData("+509 4812 3456", "+50948123456", "HT")]
    [InlineData("+509 2222 3333", "+50922223333", "HT")]
    // United States / Canada (+1)
    [InlineData("+1 (650) 253-0000", "+16502530000", "US")]
    [InlineData("+1 305 555 1234", "+13055551234", "US")]
    [InlineData("+1 514 555 0199", "+15145550199", "CA")]
    // Other countries of the diaspora
    [InlineData("+33 1 42 68 53 00", "+33142685300", "FR")]
    [InlineData("+56 2 2123 4567", "+56221234567", "CL")]
    public void Valid_numbers_are_normalized_to_E164(string input, string expected, string region)
    {
        var result = _normalizer.Normalize(input);

        Assert.NotNull(result);
        Assert.Equal(expected, result.E164);
        Assert.Equal(region, result.RegionCode);
    }

    [Theory]
    [InlineData("+509 1234")]           // too short for Haiti
    [InlineData("+509 3712 34567")]     // too long for Haiti
    [InlineData("123")]                 // too short
    [InlineData("+1 123")]              // too short for +1
    [InlineData("+1 055 253 0000")]     // US area codes never start with 0
    [InlineData("+999 1234 5678")]      // unassigned country code
    [InlineData("abc")]
    [InlineData("+509 abcd efgh")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("+509 3712 3456 3712 3456 3712 3456 3712")] // absurdly long input
    public void Invalid_numbers_are_rejected(string? input)
    {
        Assert.Null(_normalizer.Normalize(input));
    }
}
