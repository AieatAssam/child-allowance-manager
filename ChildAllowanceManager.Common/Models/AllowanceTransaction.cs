using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChildAllowanceManager.Common.Models;

[Table("allowance_transactions")]
public class AllowanceTransaction
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Column("balance")]
    public decimal Balance { get; set; }
    
    [Column("transaction_amount")]
    public decimal TransactionAmount { get; set; }
    
    [Column("description")]
    public string Description { get; set; }
    
    [Column("child_id")]
    public string ChildId { get; set; }
    
    [Column("tenant_id")]
    public string TenantId { get; set; }
    
    [Column("transaction_timestamp")]
    public DateTimeOffset TransactionTimestamp { get; set; }
    
    [Column("transaction_type")]
    public TransactionType TransactionType { get; set; }
    
    [Column("deleted")]
    public bool Deleted { get; set; } = false;
    
    [Column("created_timestamp")]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    // Add navigation properties if there are relationships to other entities
    [ForeignKey(nameof(ChildId))]
    public virtual ChildConfiguration Child { get; set; }
    
    [ForeignKey(nameof(TenantId))]
    public virtual TenantConfiguration Tenant { get; set; }
}