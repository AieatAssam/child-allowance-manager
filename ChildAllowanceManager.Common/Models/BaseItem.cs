using System.ComponentModel.DataAnnotations;

namespace ChildAllowanceManager.Common.Models;

public abstract class BaseItem
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public bool Deleted { get; set; } = false;
    
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
}
