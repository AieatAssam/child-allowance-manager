using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChildAllowanceManager.Data.PostgreSQL.Configurations
{
    public class AllowanceTransactionConfiguration : IEntityTypeConfiguration<AllowanceTransaction>
    {
        public void Configure(EntityTypeBuilder<AllowanceTransaction> builder)
        {
            builder.ToTable("allowance_transactions");
            
            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.Id)
                .HasColumnName("id")
                .IsRequired();
                
            builder.Property(x => x.Balance)
                .HasColumnName("balance")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
                
            builder.Property(x => x.TransactionAmount)
                .HasColumnName("transaction_amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
                
            builder.Property(x => x.Description)
                .HasColumnName("description")
                .HasMaxLength(500);
                
            builder.Property(x => x.ChildId)
                .HasColumnName("child_id")
                .IsRequired();
                
            builder.Property(x => x.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();
                
            builder.Property(x => x.TransactionTimestamp)
                .HasColumnName("transaction_timestamp")
                .IsRequired();
                
            builder.Property(x => x.TransactionType)
                .HasColumnName("transaction_type")
                .HasConversion<int>()
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
            builder.HasOne(x => x.Child)
                .WithMany(x => x.AllowanceTransactions)
                .HasForeignKey(x => x.ChildId)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Add indexes
            builder.HasIndex(x => x.TenantId);
            builder.HasIndex(x => x.ChildId);
            builder.HasIndex(x => x.TransactionTimestamp);
        }
    }
}