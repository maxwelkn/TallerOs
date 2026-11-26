using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerOs.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackMailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptUtc",
                table: "EmailOutbox",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE EmailOutbox SET LastError = 'Fallo SMTP registrado antes de esta actualización.' WHERE LastError IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_State",
                table: "EmailOutbox",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_State",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "LastAttemptUtc",
                table: "EmailOutbox");
        }
    }
}
