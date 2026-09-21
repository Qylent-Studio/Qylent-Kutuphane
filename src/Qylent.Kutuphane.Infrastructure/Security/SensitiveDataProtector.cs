using System.Security.Cryptography;
using System.Text;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Security;

public sealed class SensitiveDataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public SensitiveDataProtector(AppPaths paths, IKeyProtectionProvider keyProtectionProvider)
    {
        _key = LoadOrCreateKey(paths.KeyPath, keyProtectionProvider);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        var envelope = new byte[1 + NonceSize + TagSize + cipher.Length];
        envelope[0] = 1;
        nonce.CopyTo(envelope, 1);
        tag.CopyTo(envelope, 1 + NonceSize);
        cipher.CopyTo(envelope, 1 + NonceSize + TagSize);
        return Convert.ToBase64String(envelope);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        var envelope = Convert.FromBase64String(cipherText);
        if (envelope.Length < 1 + NonceSize + TagSize || envelope[0] != 1)
            throw new CryptographicException("Hassas veri biçimi desteklenmiyor.");
        var nonce = envelope.AsSpan(1, NonceSize);
        var tag = envelope.AsSpan(1 + NonceSize, TagSize);
        var cipher = envelope.AsSpan(1 + NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] LoadOrCreateKey(string keyPath, IKeyProtectionProvider keyProtectionProvider)
    {
        if (File.Exists(keyPath))
        {
            return keyProtectionProvider.Unprotect(File.ReadAllBytes(keyPath));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var protectedKey = keyProtectionProvider.Protect(key);
        var temporaryPath = keyPath + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedKey);
        File.Move(temporaryPath, keyPath, true);
        return key;
    }
}
