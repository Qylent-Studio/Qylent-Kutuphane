using System.Security.Cryptography;
using System.Text;

namespace Qylent.Kutuphane.Infrastructure.Security;

internal static class PasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (string Hash, string Salt) Hash(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(value), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string value, string hashBase64, string saltBase64)
    {
        try
        {
            var expected = Convert.FromBase64String(hashBase64);
            var salt = Convert.FromBase64String(saltBase64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(value), salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string NormalizeAnswer(string answer) => string.Join(' ', answer.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

