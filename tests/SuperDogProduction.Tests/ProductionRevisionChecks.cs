using Lumine.AuthServer.Application.DTOs.SuperDog;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure.Services;

internal static class ProductionRevisionChecks
{
    internal static void Run(ProductionUpload original, ProductionValidator validator, Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        var head = new DongleEntitlement
        {
            Id = id, VendorId = "test-vendor", ActivationSourceHardwareKeyId = original.HardwareKeyId,
            BindingState = "Bound", BindingRevision = 7
        };
        var earlier = validator.Validate(original).Record;
        // 客户端时间故意反向：生产顺序只取业务修订，不取 WrittenAt。
        var later = validator.Validate(original with
        {
            OperationId = "rewrite-2", ProductionRevision = 2, WrittenAt = original.WrittenAt.AddDays(-1)
        }).Record;
        check(head.TryAdvanceProduction(later.ProductionRevision, later.Form.Fields.ManufacturerId, "new-digest"),
            "first received revision may be greater than one");
        check(!head.TryAdvanceProduction(earlier.ProductionRevision, 5, "old-digest"),
            "late first upload of older production cannot advance head");
        check(head.ProductionRevision == 2 && head.ManufacturerId == later.Form.Fields.ManufacturerId &&
              head.PayloadDigest == "new-digest", "older material cannot overwrite manufacturer or digest");
        check(!head.TryAdvanceProduction(2, 6, "conflicting-digest") && head.PayloadDigest == "new-digest",
            "equal revision cannot mutate head");
        check(head.TryAdvanceProduction(3, 6, "third-digest") && head.ProductionRevision == 3 && head.ManufacturerId == 6,
            "newer revision advances production fields");
        check(!head.TryAdvanceProduction(1, 4, "old-digest") && !head.TryAdvanceProduction(2, 5, "new-digest") &&
              head.PayloadDigest == "third-digest", "multiple historical uploads leave latest production unchanged");
        check(head.Id == id && head.BindingState == "Bound" && head.BindingRevision == 7,
            "production updates preserve entitlement identity and binding");

        var inOrder = new DongleEntitlement();
        inOrder.TryAdvanceProduction(1, 4, "old-digest");
        inOrder.TryAdvanceProduction(3, 6, "third-digest");
        inOrder.TryAdvanceProduction(2, 5, "new-digest");
        check(inOrder.ProductionRevision == head.ProductionRevision && inOrder.ManufacturerId == head.ManufacturerId &&
              inOrder.PayloadDigest == head.PayloadDigest, "out of order uploads converge to highest production revision");
    }
}
