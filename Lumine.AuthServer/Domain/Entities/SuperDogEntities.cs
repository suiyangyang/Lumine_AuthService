namespace Lumine.AuthServer.Domain.Entities;

public sealed class ManufacturerSecret
{
    public Guid Id { get; set; }
    public string VendorId { get; set; } = "";
    public ushort ManufacturerId { get; set; }
    public int Version { get; set; } = 1;
    public string ProtectedKey { get; set; } = "";
    public string KeyLookupHmac { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class DongleEntitlement
{
    public Guid Id { get; set; }
    public string VendorId { get; set; } = "";
    public ulong ActivationSourceHardwareKeyId { get; set; }
    public ushort ManufacturerId { get; set; }
    public string PayloadDigest { get; set; } = "";
    public int ProductionRevision { get; set; }
    // 生产登记只更新生产材料；绑定状态和代次保留给后续激活业务。
    public string BindingState { get; set; } = "Unbound";
    public long BindingRevision { get; set; }

    // 接收顺序和客户端时钟均不能推进生产修订；旧材料只能作为历史登记。
    public bool TryAdvanceProduction(int revision, ushort manufacturerId, string payloadDigest)
    {
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        if (revision <= ProductionRevision) return false;
        ProductionRevision = revision;
        ManufacturerId = manufacturerId;
        PayloadDigest = payloadDigest;
        return true;
    }
}

public sealed class ProductionRegistration
{
    public Guid Id { get; set; }
    public string OperationId { get; set; } = "";
    public string VendorId { get; set; } = "";
    public string MaterialHash { get; set; } = "";
    public Guid EntitlementId { get; set; }
    // null 表示无法确定修订的存量登记，不猜测其生产顺序。
    public int? ProductionRevision { get; set; }
    public string MaterialJson { get; set; } = "";
    public string UploadedBySubject { get; set; } = "";
    public DateTime UploadedAtUtc { get; set; }
    public DongleEntitlement Entitlement { get; set; } = null!;
}
