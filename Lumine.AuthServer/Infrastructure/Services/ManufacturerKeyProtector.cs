using System.Security.Cryptography;
using System.Text;
using Lumine.AuthServer.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Lumine.AuthServer.Infrastructure.Services;

public sealed class ManufacturerKeyProtector(IOptions<SuperDogOptions> options)
{
    private byte[] GetKey()
    {
        try
        {
            var key = Convert.FromBase64String(options.Value.EncryptionKeyBase64);
            if (key.Length == 32) return key;
        }
        catch (FormatException) { }
        throw new SuperDogUnavailableException();
    }

    private static byte[] Context(ManufacturerSecret key) => Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"SuperDog41/manufacturer-key/v1/{key.VendorId}/{key.ManufacturerId}/{key.Id:D}/{key.Version}"));

    public string Protect(ManufacturerSecret key, string plainText)
    {
        var secret = GetKey();
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var encrypted = new byte[bytes.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(secret, 16);
            aes.Encrypt(nonce, bytes, encrypted, tag, Context(key));
            return Convert.ToBase64String([.. nonce, .. tag, .. encrypted]);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public string Unprotect(ManufacturerSecret key)
    {
        var secret = GetKey();
        try
        {
            var envelope = Convert.FromBase64String(key.ProtectedKey);
            if (envelope.Length < 29) throw new CryptographicException();
            var bytes = new byte[envelope.Length - 28];
            using var aes = new AesGcm(secret, 16);
            aes.Decrypt(envelope.AsSpan(0, 12), envelope.AsSpan(28), envelope.AsSpan(12, 16), bytes, Context(key));
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        { throw new SuperDogUnavailableException(); }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public string Lookup(string vendorId, string plainText)
    {
        var secret = GetKey();
        try
        {
            var derived = HMACSHA256.HashData(secret, "SuperDog41/key-lookup/v1"u8);
            try { return Convert.ToHexString(HMACSHA256.HashData(derived, Encoding.UTF8.GetBytes(vendorId + ":" + plainText))); }
            finally { CryptographicOperations.ZeroMemory(derived); }
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }
}
