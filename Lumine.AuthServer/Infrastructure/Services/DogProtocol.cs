// 协议快照：SuperDog41.Core/DogProtocol.cs；验签规则须与生产端同步。
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

using Lumine.AuthServer.Application.DTOs.SuperDog;

namespace Lumine.AuthServer.Infrastructure.Services;

/// <summary>只处理内存中的协议字节；签名算法与受信密钥由外部签名器提供。</summary>
public sealed class DogProtocol
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string[] Attributes = ["v", "sn", "m", "e", "a", "c", "n"];
    private readonly HashSet<ushort> manufacturerIds;
    private readonly IDogSigner signer;

    public DogProtocol(IReadOnlySet<ushort> manufacturerIds, IDogSigner signer)
    {
        ArgumentNullException.ThrowIfNull(manufacturerIds);
        ArgumentNullException.ThrowIfNull(signer);
        if (manufacturerIds.Count == 0 || manufacturerIds.Contains(0))
            throw new ArgumentException("厂家字典不能为空或包含编号零。", nameof(manufacturerIds));
        this.manufacturerIds = new HashSet<ushort>(manufacturerIds);
        this.signer = signer;
    }

    public SignedDog Encode(ulong hardwareKeyId, DogFields fields)
    {
        var xml = Canonical(fields);
        if (xml.Length is < 1 or > 112) throw Invalid("XML 长度超出容量。");
        // V1 仅发行受信公钥编号 1；增加编号须同步升级受信配置。
        if (signer.KeyId != 1) throw Invalid("不支持的签名公钥编号。");
        var ro = new byte[112];
        var rw = new byte[112];
        "SD41"u8.CopyTo(ro);
        ro[4] = 1;
        ro[5] = signer.KeyId;
        BinaryPrimitives.WriteUInt16LittleEndian(ro.AsSpan(6, 2), (ushort)xml.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(ro.AsSpan(8, 8), hardwareKeyId);
        xml.CopyTo(rw, 0);
        var bytes = SignedBytes(ro, rw, xml.Length);
        // Sign/Verify 接收未哈希的消息，外部实现使用 P-256/SHA-256/P1363。
        var signature = signer.Sign((byte[])bytes.Clone());
        if (signature is null || signature.Length != 64) throw Invalid("生产签名必须是64字节 P1363 格式。");
        signature.CopyTo(ro, 16);
        return new SignedDog(ro, rw, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public DogFields Decode(ulong hardwareKeyId, byte[] readOnly, byte[] readWrite)
    {
        ArgumentNullException.ThrowIfNull(readOnly);
        ArgumentNullException.ThrowIfNull(readWrite);
        if (readOnly.Length != 112 || readWrite.Length != 112) throw Invalid("RO/RW 必须分别为112字节。");
        // 固定快照，避免调用方或外部签名器修改正在验证的材料。
        var ro = (byte[])readOnly.Clone();
        var rw = (byte[])readWrite.Clone();
        if (!ro.AsSpan(0, 4).SequenceEqual("SD41"u8) || ro[4] != 1 || ro[5] != 1)
            throw Invalid("未知 Magic、封装版本或签名公钥编号。");
        int length = BinaryPrimitives.ReadUInt16LittleEndian(ro.AsSpan(6, 2));
        if (length is < 1 or > 112 || ro.AsSpan(80).ContainsAnyExcept((byte)0) || rw.AsSpan(length).ContainsAnyExcept((byte)0))
            throw Invalid("XML 长度或文件尾部补零无效。");
        if (!signer.Verify(ro[5], SignedBytes(ro, rw, length), ro.AsSpan(16, 64).ToArray()))
            throw Invalid("生产签名验证失败。");
        if (BinaryPrimitives.ReadUInt64LittleEndian(ro.AsSpan(8, 8)) != hardwareKeyId)
            throw Invalid("签名绑定的硬件 ID 与实际设备不符。");

        string text = Utf8.GetString(rw, 0, length);
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 112,
            MaxCharactersFromEntities = 112,
            IgnoreComments = false,
            IgnoreWhitespace = false,
            ConformanceLevel = ConformanceLevel.Document
        });
        if (!reader.Read() || reader.NodeType != XmlNodeType.Element || reader.Name != "Dog" ||
            reader.NamespaceURI.Length != 0 || !reader.IsEmptyElement || reader.AttributeCount != Attributes.Length)
            throw Invalid("必须是仅含七个属性的空 Dog 根节点。");
        var values = new string[Attributes.Length];
        for (int i = 0; i < Attributes.Length; i++)
        {
            reader.MoveToAttribute(i);
            if (reader.Name != Attributes[i] || reader.NamespaceURI.Length != 0)
                throw Invalid("XML 属性名称或顺序无效。");
            values[i] = reader.Value;
        }
        reader.MoveToElement();
        if (reader.Read()) throw Invalid("Dog 节点后不允许附加内容。");
        if (!Enum.TryParse<ApplicationType>(values[4], false, out var application) ||
            !Enum.TryParse<ClientType>(values[5], false, out var client))
            throw Invalid("未知应用类型或客户端类型。");
        var fields = new DogFields(Number(values[0]), values[1], Number(values[2]),
            (Edition)Number(values[3]), application, client, Number(values[6]));
        // 解析只用于读取字段；验签始终使用上述原字节。逐字节比对拒绝实体、BOM、
        // 声明、单双引号变化、额外空白和非规范数字等所有非 canonical 表达。
        if (!Canonical(fields).AsSpan().SequenceEqual(rw.AsSpan(0, length)))
            throw Invalid("XML 不是规范的 UTF-8 七字段编码。");
        return fields;
    }

    private byte[] Canonical(DogFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.InfoVersion != 1 || !manufacturerIds.Contains(fields.ManufacturerId) ||
            fields.ManufacturerId == 0 || fields.MaxInstrumentSets == 0 ||
            !Enum.IsDefined(fields.Edition) || !Enum.IsDefined(fields.ApplicationType) || !Enum.IsDefined(fields.ClientType))
            throw Invalid("未知版本、厂家、枚举或无效仪器套数。");
        if (fields.SerialNumber is null) throw Invalid("序列号不能为空。");
        SerialNumbers.Parse(fields.SerialNumber);
        return Utf8.GetBytes(FormattableString.Invariant(
            $"<Dog v=\"1\" sn=\"{fields.SerialNumber}\" m=\"{fields.ManufacturerId}\" e=\"{(int)fields.Edition}\" a=\"{fields.ApplicationType}\" c=\"{fields.ClientType}\" n=\"{fields.MaxInstrumentSets}\"/>"));
    }

    private static ushort Number(string value) => ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        ? number : throw Invalid("数值字段必须是 UInt16 十进制数字。");

    private static byte[] SignedBytes(byte[] ro, byte[] rw, int length)
    {
        var bytes = new byte[16 + length];
        ro.AsSpan(0, 16).CopyTo(bytes);
        rw.AsSpan(0, length).CopyTo(bytes.AsSpan(16));
        return bytes;
    }

    private static InvalidDataException Invalid(string message) => new(message);
}


public sealed record SignedDog(byte[] ReadOnly, byte[] ReadWrite, string PayloadDigest);
public interface IDogSigner
{
    byte KeyId { get; }
    byte[] Sign(byte[] bytes);
    bool Verify(byte keyId, byte[] bytes, byte[] signature);
}
