using System.Security.Cryptography;
using System.Text;

namespace Imotek.Application.Requests;

/// <summary>
/// The secret in a visitor's follow-up link: 32 random bytes, URL-safe. Only its SHA-256 is stored,
/// so a database leak does not give access to the requests.
/// </summary>
public static class GuestAccessToken
{
    private const int ByteLength = 32;
    private const int TextLength = 43; // base64url of 32 bytes, without padding

    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(ByteLength))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    public static bool IsWellFormed(string? token) =>
        token is { Length: TextLength } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
