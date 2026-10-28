using System.Net;
using System.Security.Cryptography;

namespace Identity.IntegrationTests.Infrastructure;

public static class TotpHelper
{
    public static string GenerateCode(string base32Key, DateTimeOffset? time = null)
    {
        var unixTime = (time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var timestep = unixTime / 30;

        var key = Base32Decode(base32Key);
        var timestepBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(timestep));

#pragma warning disable CA5350 // RFC 6238 standard TOTP specifies HMAC-SHA1
        using var hmac = new HMACSHA1(key);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(timestepBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        var cleaned = input.TrimEnd('=').ToUpperInvariant();
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in cleaned)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
