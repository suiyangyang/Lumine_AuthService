using Lumine.AuthServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure;

internal static class SuperDogModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ManufacturerSecret>(b =>
        {
            b.ToTable("ManufacturerKeys");
            b.HasKey(x => x.Id);
            b.Property(x => x.VendorId).HasMaxLength(64).UseCollation("utf8mb4_bin");
            b.Property(x => x.ProtectedKey).HasMaxLength(1024);
            b.Property(x => x.KeyLookupHmac).HasMaxLength(64);
            b.HasIndex(x => new { x.VendorId, x.ManufacturerId }).IsUnique();
            b.HasIndex(x => x.KeyLookupHmac).IsUnique();
        });
        model.Entity<DongleEntitlement>(b =>
        {
            b.ToTable("DongleEntitlements");
            b.HasKey(x => x.Id);
            b.Property(x => x.VendorId).HasMaxLength(64).UseCollation("utf8mb4_bin");
            b.Property(x => x.PayloadDigest).HasMaxLength(64);
            b.Property(x => x.ProductionRevision).IsConcurrencyToken();
            b.Property(x => x.BindingState).HasMaxLength(32);
            b.HasIndex(x => new { x.VendorId, x.ActivationSourceHardwareKeyId }).IsUnique();
        });
        model.Entity<ProductionRegistration>(b =>
        {
            b.ToTable("ProductionRegistrations");
            b.HasKey(x => x.Id);
            b.Property(x => x.OperationId).HasMaxLength(96).UseCollation("utf8mb4_bin");
            b.Property(x => x.VendorId).HasMaxLength(64).UseCollation("utf8mb4_bin");
            b.Property(x => x.MaterialHash).HasMaxLength(64);
            b.Property(x => x.MaterialJson).HasColumnType("longtext");
            b.Property(x => x.UploadedBySubject).HasMaxLength(64);
            b.HasIndex(x => new { x.VendorId, x.OperationId }).IsUnique();
            b.HasIndex(x => new { x.EntitlementId, x.ProductionRevision }).IsUnique();
            b.HasOne(x => x.Entitlement).WithMany().HasForeignKey(x => x.EntitlementId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
