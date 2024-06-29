using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Common.Models;

public abstract class BaseItem : Item
{
    public bool Deleted { get; set; } = false;
    
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
}