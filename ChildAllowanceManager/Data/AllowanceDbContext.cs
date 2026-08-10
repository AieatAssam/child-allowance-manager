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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureItem(modelBuilder.Entity<ChildConfiguration>());
        ConfigureItem(modelBuilder.Entity<AllowanceTransaction>());
        ConfigureItem(modelBuilder.Entity<TenantConfiguration>());
        ConfigureItem(modelBuilder.Entity<User>());

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
            entity.HasIndex(x => x.UrlSuffix).IsUnique();
            entity.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(64).HasDefaultValue("Europe/London");
        });
        modelBuilder.Entity<User>(entity => entity.HasIndex(x => x.Email).IsUnique());
    }

    private static void ConfigureItem<T>(EntityTypeBuilder<T> entity) where T : BaseItem
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CreatedTimestamp).IsRequired();
        entity.Property(x => x.UpdatedTimestamp).IsRequired();
    }
}
