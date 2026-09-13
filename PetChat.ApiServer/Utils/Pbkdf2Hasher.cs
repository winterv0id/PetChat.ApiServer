using System.Security.Cryptography;

namespace PetChat.ApiServer.Utils;

public class Pbkdf2Hasher
{
    private const int SaltSize = 16; // 128 бит
    private const int HashSize = 32; // 256 бит
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, HashSize);

        // соль:хэш
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHashWithSalt)
    {
        string[] parts = storedHashWithSalt.Split(':');
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] storedHash = Convert.FromBase64String(parts[1]);

        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
}