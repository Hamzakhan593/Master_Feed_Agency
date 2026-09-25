using Master_Feed_Agency.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLedgerEntry> CustomerLedgerEntries => Set<CustomerLedgerEntry>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    // Posted records are corrected through reversal entries, never deleted.
    private void ProtectRecords()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(x => x.State == EntityState.Deleted &&
            x.Entity is Sale or SaleItem or Payment or PaymentAllocation or CustomerLedgerEntry or StockTransaction))
            throw new InvalidOperationException("Saved sales, payments and stock records cannot be deleted. Use cancellation instead.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.IsActive);
        });

        builder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PurchaseCost).HasPrecision(18, 2);
            entity.Property(x => x.DefaultSalePrice).HasPrecision(18, 2);
            entity.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.Name });
        });

        builder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.BusinessName).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CnicOrIdentifier).HasMaxLength(25);
            entity.Property(x => x.Area).HasMaxLength(120);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
            entity.Property(x => x.GuarantorName).HasMaxLength(150);
            entity.Property(x => x.GuarantorPhone).HasMaxLength(30);
            entity.Property(x => x.DeliveryNotes).HasMaxLength(500);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Phone);
            entity.HasIndex(x => new { x.IsActive, x.Name });
        });

        builder.Entity<CustomerLedgerEntry>(entity =>
        {
            entity.Property(x => x.Debit).HasPrecision(18, 2);
            entity.Property(x => x.Credit).HasPrecision(18, 2);
            entity.Property(x => x.ReferenceType).HasMaxLength(50);
            entity.Property(x => x.ReferenceId).HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(x => new { x.CustomerId, x.Date });

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.LedgerEntries)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockTransaction>(entity =>
        {
            entity.Property(x => x.QuantityIn).HasPrecision(18, 3);
            entity.Property(x => x.QuantityOut).HasPrecision(18, 3);
            entity.Property(x => x.ReferenceType).HasMaxLength(50);
            entity.Property(x => x.ReferenceId).HasMaxLength(100);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ApprovedByUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.ProductId, x.Date });

            entity.HasOne(x => x.Product)
                .WithMany(x => x.StockTransactions)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Sale>(entity =>
        {
            entity.Property(x => x.InvoiceNo).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ClientRequestId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.GrossTotal).HasPrecision(18, 2);
            entity.Property(x => x.Discount).HasPrecision(18, 2);
            entity.Property(x => x.NetTotal).HasPrecision(18, 2);
            entity.Property(x => x.PaidAtSale).HasPrecision(18, 2);
            entity.Property(x => x.CreditAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
            entity.Property(x => x.CancelReason).HasMaxLength(500);
            entity.HasIndex(x => x.InvoiceNo).IsUnique();
            entity.HasIndex(x => x.ClientRequestId).IsUnique();
            entity.HasIndex(x => new { x.SaleDate, x.Status });
            entity.HasIndex(x => new { x.CustomerId, x.SaleDate });

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Rate).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasIndex(x => x.ProductId);

            entity.HasOne(x => x.Sale)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.ReceiptNo).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ClientRequestId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ReceivedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.ReversedByUserId).HasMaxLength(450);
            entity.Property(x => x.ReversalReason).HasMaxLength(500);
            entity.HasIndex(x => x.ReceiptNo).IsUnique();
            entity.HasIndex(x => x.ClientRequestId).IsUnique();
            entity.HasIndex(x => new { x.CustomerId, x.PaymentDate });

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentAllocation>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.PaymentId);
            entity.HasIndex(x => x.SaleId);

            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Sale)
                .WithMany()
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
        });







    }
}
