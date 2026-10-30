namespace Lumine.AuthServer.Infrastructure.Services;

public sealed class SuperDogOptions
{
    public const string SectionName = "SuperDog";
    public const string ClientId = "superdog41";
    // 空配置关闭生产操作，不生成环境密钥或信任客户端公钥。
    public string VendorId { get; set; } = "";
    public string EncryptionKeyBase64 { get; set; } = "";
    public ushort[] ManufacturerIds { get; set; } = Enumerable.Range(1, 32).Select(i => (ushort)i).ToArray();
    public List<SuperDogSigningKey> TrustedSigningKeys { get; set; } = [];
}

public sealed class SuperDogSigningKey
{
    public string VendorId { get; set; } = "";
    public byte SigningKeyId { get; set; }
    public string PublicKeyPem { get; set; } = "";
}

public sealed class SuperDogUnavailableException : Exception
{
    public SuperDogUnavailableException() : base("SuperDog 受控配置或密钥不可用，请联系管理员。") { }
}
