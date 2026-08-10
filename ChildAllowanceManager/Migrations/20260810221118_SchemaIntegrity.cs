using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildAllowanceManager.Migrations
{
    /// <inheritdoc />
    public partial class SchemaIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_UrlSuffix",
                table: "Tenants");

            migrationBuilder.AddColumn<string>(
                name: "ActorEmail",
                table: "Transactions",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "Transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Allowance schedule");

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                table: "Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "Transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversesTransactionId",
                table: "Transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/London");

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Deliberate cleanup of pre-existing orphan rows before foreign keys are enforced.
            migrationBuilder.Sql("DELETE FROM \"Transactions\" t WHERE NOT EXISTS (SELECT 1 FROM \"Children\" c WHERE c.\"Id\" = t.\"ChildId\");");
            migrationBuilder.Sql("DELETE FROM \"Children\" c WHERE NOT EXISTS (SELECT 1 FROM \"Tenants\" t WHERE t.\"Id\" = c.\"TenantId\");");
            migrationBuilder.Sql("DELETE FROM \"Transactions\" t WHERE NOT EXISTS (SELECT 1 FROM \"Tenants\" x WHERE x.\"Id\" = t.\"TenantId\");");

            migrationBuilder.Sql("INSERT INTO \"TenantMemberships\" (\"Id\", \"UserId\", \"TenantId\", \"Role\", \"Deleted\", \"CreatedTimestamp\", \"UpdatedTimestamp\") SELECT replace(gen_random_uuid()::text, '-', ''), u.\"Id\", t.tenant_id, 'parent', false, now(), now() FROM \"Users\" u CROSS JOIN LATERAL unnest(u.\"Tenants\") AS t(tenant_id) WHERE NOT u.\"Deleted\" AND EXISTS (SELECT 1 FROM \"Tenants\" x WHERE x.\"Id\" = t.tenant_id) ON CONFLICT DO NOTHING;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "NOT \"Deleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ChildId",
                table: "Transactions",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReversesTransactionId",
                table: "Transactions",
                column: "ReversesTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_RequestId",
                table: "Transactions",
                columns: new[] { "TenantId", "RequestId" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_UrlSuffix",
                table: "Tenants",
                column: "UrlSuffix",
                unique: true,
                filter: "NOT \"Deleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId",
                table: "TenantMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId_TenantId",
                table: "TenantMemberships",
                columns: new[] { "UserId", "TenantId" },
                unique: true,
                filter: "NOT \"Deleted\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_Tenants_TenantId",
                table: "Children",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Children_ChildId",
                table: "Transactions",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Tenants_TenantId",
                table: "Transactions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_Tenants_TenantId",
                table: "Children");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Children_ChildId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Tenants_TenantId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "TenantMemberships");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ChildId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReversesTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_RequestId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_UrlSuffix",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ActorEmail",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReversesTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Tenants");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_UrlSuffix",
                table: "Tenants",
                column: "UrlSuffix",
                unique: true);
        }
    }
}
