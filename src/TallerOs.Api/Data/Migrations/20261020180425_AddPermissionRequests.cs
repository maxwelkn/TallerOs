using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerOs.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolverId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionRequests_RequesterId_Status",
                table: "PermissionRequests",
                columns: new[] { "RequesterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_Permission",
                table: "UserPermissions",
                columns: new[] { "UserId", "Permission" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionRequests");

            migrationBuilder.DropTable(
                name: "UserPermissions");
        }
    }
}
