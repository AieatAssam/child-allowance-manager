using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data.PostgreSQL.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildAllowanceManager.Data.PostgreSQL;

public class DataContext : DbContext
{
    private readonly IConfiguration _configuration;
    
    public DataContext(DbContextOptions<DataContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }
    
    public DbSet<AllowanceTransaction> AllowanceTransactions { get; set; }
    // Add other DbSets for your entities
    public DbSet<ChildConfiguration> Children { get; set; }
    public DbSet<TenantConfiguration> Tenants { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_configuration.GetConnectionString("DefaultConnection"));
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configurations
        modelBuilder.ApplyConfiguration(new AllowanceTransactionConfiguration());
    }
}