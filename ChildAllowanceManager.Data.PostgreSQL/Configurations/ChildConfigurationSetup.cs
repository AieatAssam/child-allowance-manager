using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data.PostgreSQL.Configurations;

public class ChildConfigurationSetup : IEntityTypeConfiguration<ChildConfiguration>
{
    public void Configure(EntityTypeBuilder<ChildConfiguration> builder)
    {
        builder.ToTable("children");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();
            
        builder.Property(x => x.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(x => x.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(x => x.BirthDate)
            .HasColumnName("birth_date");
            
        builder.Property(x => x.RegularAllowance)
            .HasColumnName("regular_allowance")
            .HasColumnType("decimal(18,2)")
            .IsRequired();
            
        builder.Property(x => x.HoldDaysRemaining)
            .HasColumnName("hold_days_remaining")
            .IsRequired();
            
        builder.Property(x => x.BirthdayAllowance)
            .HasColumnName("birthday_allowance")
            .HasColumnType("decimal(18,2)");
            
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
            
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
        
        // Relationships
        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(x => x.AllowanceTransactions)
            .WithOne(x => x.Child)
            .HasForeignKey(x => x.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Add indexes
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.FirstName);
        builder.HasIndex(x => x.LastName);
    }
} 