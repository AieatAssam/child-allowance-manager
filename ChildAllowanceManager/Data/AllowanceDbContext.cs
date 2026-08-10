using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data;

public sealed class AllowanceDbContext(DbContextOptions<AllowanceDbContext> options) : DbContext(options)
{
    public DbSet<ChildConfiguration> Children => Set<ChildConfiguration>();
    public DbSet<AllowanceTransaction> Transactions => Set<AllowanceTransaction>();
    public DbSet<TenantConfiguration> Tenants => Set<TenantConfiguration>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureItem(modelBuilder.Entity<ChildConfiguration>());
        ConfigureItem(modelBuilder.Entity<AllowanceTransaction>());
        ConfigureItem(modelBuilder.Entity<TenantConfiguration>());
        ConfigureItem(modelBuilder.Entity<User>());
        ConfigureItem(modelBuilder.Entity<TenantMembership>());

        modelBuilder.Entity<ChildConfiguration>(entity =>
        {
            entity.Property(x => x.BirthDate).HasColumnType("date");
            entity.Property(x => x.RegularAllowance).HasPrecision(18, 2);
            entity.Property(x => x.BirthdayAllowance).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.Deleted });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AllowanceTransaction>(entity =>
        {
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.Property(x => x.TransactionAmount).HasPrecision(18, 2);
            entity.Property(x => x.AllowanceDate).HasColumnType("date");
            entity.HasIndex(x => new { x.TenantId, x.ChildId, x.TransactionTimestamp });
            entity.HasIndex(x => new { x.TenantId, x.ChildId, x.AllowanceDate })
                .IsUnique()
                .HasFilter("\"AllowanceDate\" IS NOT NULL");
            entity.HasOne(x => x.Child)
                .WithMany()
                .HasForeignKey(x => x.ChildId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TenantConfiguration>(entity =>
        {
            entity.HasIndex(x => x.UrlSuffix).IsUnique().HasFilter("NOT \"Deleted\"");
            entity.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(64).HasDefaultValue("Europe/London");
        });
        modelBuilder.Entity<User>(entity => entity.HasIndex(x => x.Email).IsUnique().HasFilter("NOT \"Deleted\""));
        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.Property(x => x.Role).IsRequired().HasMaxLength(32);
            entity.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique().HasFilter("NOT \"Deleted\"");
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureItem<T>(EntityTypeBuilder<T> entity) where T : BaseItem
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CreatedTimestamp).IsRequired();
        entity.Property(x => x.UpdatedTimestamp).IsRequired();
    }
}
