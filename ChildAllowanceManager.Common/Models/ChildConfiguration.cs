using System.ComponentModel.DataAnnotations;

namespace ChildAllowanceManager.Common.Models;

public class ChildConfiguration : BaseItem
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }
    
    [DataType(DataType.Currency)]
    public decimal RegularAllowance { get; set; } = 1.0m;
    
    public int HoldDaysRemaining { get; set; } = 0;

    [DataType(DataType.Currency)] public decimal? BirthdayAllowance { get; set; } = null;
    
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    
}
