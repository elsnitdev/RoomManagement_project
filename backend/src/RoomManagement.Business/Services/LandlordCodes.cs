using System.Security.Cryptography;

namespace RoomManagement.Business.Services;

public static class LandlordCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string New()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        return "L" + new string(bytes.ToArray().Select(b => Alphabet[b % Alphabet.Length]).ToArray());
    }
}
