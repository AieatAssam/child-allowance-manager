using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data.PostgreSQL.Configurations;

public class UserTenantSetup : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("user_tenants");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();
            
        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();
            
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
            
        builder.Property(x => x.CreatedTimestamp)
            .HasColumnName("created_timestamp")
            .HasDefaultValueSql("now()")
            .IsRequired();
            
        builder.Property(x => x.UpdatedTimestamp)
            .HasColumnName("updated_timestamp")
            .HasDefaultValueSql("now()")
            .IsRequired();
            
        // Configure relationships
        builder.HasOne(x => x.User)
            .WithMany(x => x.UserTenants)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.UserTenants)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Add indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
    }
} 