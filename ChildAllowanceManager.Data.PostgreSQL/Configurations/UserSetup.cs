using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data.PostgreSQL.Configurations;

public class UserSetup : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();
            
        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
            
        builder.Property(x => x.Roles)
            .HasColumnName("roles")
            .HasColumnType("text[]");
            
        builder.Property(x => x.Tenants)
            .HasColumnName("tenants")
            .HasColumnType("text[]");
            
        builder.Property(x => x.LastLoggedIn)
            .HasColumnName("last_logged_in");
            
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
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Name);
        
        // Configure many-to-many relationship
        builder.HasMany(x => x.UserTenants)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 