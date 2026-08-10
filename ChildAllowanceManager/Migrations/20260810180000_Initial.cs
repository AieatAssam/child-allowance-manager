using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChildAllowanceManager.Migrations;

[DbContext(typeof(AllowanceDbContext))]
[Migration("20260810180000_Initial")]
public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Children",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FirstName = table.Column<string>(type: "text", nullable: false),
                LastName = table.Column<string>(type: "text", nullable: false),
                BirthDate = table.Column<DateTime>(type: "date", nullable: true),
                RegularAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                HoldDaysRemaining = table.Column<int>(type: "integer", nullable: false),
                BirthdayAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                TenantId = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Children", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Tenants",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TenantName = table.Column<string>(type: "text", nullable: false),
                UrlSuffix = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Tenants", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Roles = table.Column<string[]>(type: "text[]", nullable: false),
                Tenants = table.Column<string[]>(type: "text[]", nullable: false),
                LastLoggedIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TransactionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                ChildId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                TransactionTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TransactionType = table.Column<int>(type: "integer", nullable: false),
                AllowanceDate = table.Column<DateTime>(type: "date", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Transactions", x => x.Id));

        migrationBuilder.CreateIndex("IX_Children_TenantId_Deleted", "Children", new[] { "TenantId", "Deleted" });
        migrationBuilder.CreateIndex("IX_Tenants_UrlSuffix", "Tenants", "UrlSuffix", unique: true);
        migrationBuilder.CreateIndex("IX_Users_Email", "Users", "Email", unique: true);
        migrationBuilder.CreateIndex(
            "IX_Transactions_TenantId_ChildId_TransactionTimestamp",
            "Transactions",
            new[] { "TenantId", "ChildId", "TransactionTimestamp" });
        migrationBuilder.CreateIndex(
            "IX_Transactions_TenantId_ChildId_AllowanceDate",
            "Transactions",
            new[] { "TenantId", "ChildId", "AllowanceDate" },
            unique: true,
            filter: "\"AllowanceDate\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Children");
        migrationBuilder.DropTable("Transactions");
        migrationBuilder.DropTable("Tenants");
        migrationBuilder.DropTable("Users");
    }

    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
    }
}
