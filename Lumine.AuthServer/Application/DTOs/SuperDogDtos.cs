using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Lumine.AuthServer.Application.DTOs.SuperDog;

// 与 SuperDog41.Core/Models.cs 的线上契约一致；枚举默认按数字传输。
public enum Edition { Pro = 0, Gpc = 1, Server = 2 }
public enum ClientType { PC, Pad }
public enum ApplicationType { LC, GC, Flash }
public sealed record DogFields(int InfoVersion, string SerialNumber, ushort ManufacturerId,
    Edition Edition, ApplicationType ApplicationType, ClientType ClientType, ushort MaxInstrumentSets);
public sealed record WriteForm(DogFields Fields, string OperatorPinyin);
public sealed record ProductionUpload(string OperationId, string BatchId, ulong HardwareKeyId, WriteForm Form,
    string ReadOnlyBase64, string ReadWriteBase64, string PayloadDigest, string KeyId, int KeyVersion,
    string WrittenBySubject, string WrittenByUserName, DateTimeOffset WrittenAt, int ProductionRevision);
public sealed record UploadResult(string OperationId, bool Success, string? EntitlementId, string? Error);
public sealed record ManufacturerKey(ushort ManufacturerId, string KeyId, int Version, string PlainText);
public sealed record ManufacturerKeyRequest(ushort ManufacturerId);
public sealed record ManufacturerKeyInfo(ushort ManufacturerId, string KeyId, int Version);
public sealed record ProductionBatchRequest([property: ValidateNever] IReadOnlyList<ProductionUpload> Records);
public sealed record ProductionBatchResponse(IReadOnlyList<UploadResult> Results);

internal static class SerialNumbers
{
    public static ulong Parse(string serial)
    {
        if (serial.Length != 10 || serial.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("序列号必须是十位数字。");
        return ulong.Parse(serial, CultureInfo.InvariantCulture);
    }
}
