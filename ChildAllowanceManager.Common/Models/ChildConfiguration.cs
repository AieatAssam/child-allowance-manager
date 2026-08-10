using System.ComponentModel.DataAnnotations;

namespace ChildAllowanceManager.Common.Models;

public class ChildConfiguration : BaseItem
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }
    
    [DataType(DataType.Currency)]
    public decimal RegularAllowance { get; set; } = 1.0m;
    
    public int HoldDaysRemaining { get; set; } = 0;

    [DataType(DataType.Currency)] public decimal? BirthdayAllowance { get; set; } = null;
    
    public string TenantId { get; set; } = string.Empty;
    
}
