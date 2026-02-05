using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gekko.Waybills.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutionLocks",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LockName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcquiredBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLocks", x => new { x.TenantId, x.LockName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLocks_TenantId_LockName",
                table: "ExecutionLocks",
                columns: new[] { "TenantId", "LockName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionLocks");
        }
    }
}
