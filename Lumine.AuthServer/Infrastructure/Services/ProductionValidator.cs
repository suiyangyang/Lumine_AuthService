using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Lumine.AuthServer.Application.DTOs.SuperDog;
using Microsoft.Extensions.Options;

namespace Lumine.AuthServer.Infrastructure.Services;

public sealed record ValidatedProduction(ProductionUpload Record, string MaterialJson, string MaterialHash);

public sealed class ProductionValidator(IOptions<SuperDogOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string VendorId
    {
        get
        {
            var vendor = options.Value.VendorId;
            if (string.IsNullOrWhiteSpace(vendor) || vendor.Length > 64 || vendor != vendor.Trim())
                throw new SuperDogUnavailableException();
            return vendor;
        }
    }

    public void ValidateManufacturer(ushort id)
    {
        _ = VendorId;
        if (id == 0 || !options.Value.ManufacturerIds.Contains(id))
            throw new InvalidDataException("未知生产厂家。");
    }

    public ValidatedProduction Validate(ProductionUpload record)
    {
        if (record is null || record.Form?.Fields is null ||
            !Identifier(record.OperationId, 96) || !Identifier(record.BatchId, 96) ||
            record.HardwareKeyId == 0 || !Identifier(record.KeyId, 64) || record.KeyVersion < 1 || record.ProductionRevision < 1 ||
            !Guid.TryParse(record.WrittenBySubject, out _) || string.IsNullOrWhiteSpace(record.WrittenByUserName) ||
            record.WrittenByUserName.Length > 128 || record.WrittenAt == default ||
            record.WrittenAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidDataException("生产记录元数据无效。");
        if (record.Form.OperatorPinyin is not { Length: >= 1 and <= 100 } name || !name.All(c => c >= 'A' && c <= 'Z'))
            throw new ArgumentException("烧录人拼音必须为1—100个大写英文字母。", nameof(WriteForm.OperatorPinyin));
        ValidateManufacturer(record.Form.Fields.ManufacturerId);
        if (record.ReadOnlyBase64?.Length != 152 || record.ReadWriteBase64?.Length != 152 || record.PayloadDigest?.Length != 64)
            throw new InvalidDataException("生产材料长度无效。");
        var ro = Convert.FromBase64String(record.ReadOnlyBase64);
        var rw = Convert.FromBase64String(record.ReadWriteBase64);
        if (ro.Length != 112 || rw.Length != 112) throw new InvalidDataException("生产材料长度无效。");
        // 仅使用服务端 vendor + RO 中 signingKeyId 对应的受控 P-256 公钥。
        var keys = options.Value.TrustedSigningKeys.Where(k => k.VendorId == VendorId && k.SigningKeyId == ro[5]).ToArray();
        if (keys.Length != 1) throw new SuperDogUnavailableException();
        using var signer = new PublicSigner(keys[0]);
        var protocol = new DogProtocol(options.Value.ManufacturerIds.ToHashSet(), signer);
        var fields = protocol.Decode(record.HardwareKeyId, ro, rw);
        if (fields != record.Form.Fields) throw new InvalidDataException("签名七字段与生产表单不一致。");
        int length = BinaryPrimitives.ReadUInt16LittleEndian(ro.AsSpan(6, 2));
        var signed = new byte[16 + length];
        ro.AsSpan(0, 16).CopyTo(signed);
        rw.AsSpan(0, length).CopyTo(signed.AsSpan(16));
        var digest = Convert.ToHexString(SHA256.HashData(signed));
        if (!string.Equals(digest, record.PayloadDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("生产摘要不一致。");
        var normalized = record with
        {
            ReadOnlyBase64 = Convert.ToBase64String(ro), ReadWriteBase64 = Convert.ToBase64String(rw),
            PayloadDigest = digest, WrittenAt = record.WrittenAt.ToUniversalTime()
        };
        var json = JsonSerializer.Serialize(normalized, Json);
        return new(normalized, json, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))));
    }

    private static bool Identifier(string? value, int maximum) => !string.IsNullOrEmpty(value) && value.Length <= maximum &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    private sealed class PublicSigner : IDogSigner, IDisposable
    {
        private readonly ECDsa key = ECDsa.Create();
        public byte KeyId { get; }
        public PublicSigner(SuperDogSigningKey config)
        {
            KeyId = config.SigningKeyId;
            try
            {
                var publicKey = config.PublicKeyPem?.Trim();
                if (string.IsNullOrEmpty(publicKey)) throw new CryptographicException();
                if (publicKey.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal))
                {
                    key.ImportFromPem(publicKey);
                }
                else
                {
                    var der = Convert.FromBase64String(publicKey);
                    key.ImportSubjectPublicKeyInfo(der, out int consumed);
                    if (consumed != der.Length) throw new CryptographicException();
                }
                if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new CryptographicException();
            }
            catch (Exception e) when (e is CryptographicException or ArgumentException or FormatException)
            { key.Dispose(); throw new SuperDogUnavailableException(); }
        }
        public byte[] Sign(byte[] bytes) => throw new NotSupportedException("服务端不持有生产私钥。");
        public bool Verify(byte keyId, byte[] bytes, byte[] signature) => keyId == KeyId &&
            key.VerifyData(bytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() => key.Dispose();
    }
}
