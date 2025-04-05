using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data.PostgreSQL.Configurations;

public class TenantConfigurationSetup : IEntityTypeConfiguration<TenantConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantConfiguration> builder)
    {
        builder.ToTable("tenants");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();
            
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
            
        builder.Property(x => x.Deleted)
            .HasColumnName("deleted")
            .HasDefaultValue(false)
            .IsRequired();
            
        builder.Property(x => x.CreatedTimestamp)
            .HasColumnName("created_timestamp")
            .HasDefaultValueSql("now()")
            .IsRequired();
            
        builder.Property(x => x.UpdatedTimestamp)
            .HasColumnName("updated_timestamp")
            .HasDefaultValueSql("now()")
            .IsRequired();
            
        // Add indexes
        builder.HasIndex(x => x.Name).IsUnique();
        
        // Configure one-to-many relationships
        builder.HasMany(x => x.Children)
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(x => x.AllowanceTransactions)
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Configure many-to-many relationship
        builder.HasMany(x => x.UserTenants)
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 