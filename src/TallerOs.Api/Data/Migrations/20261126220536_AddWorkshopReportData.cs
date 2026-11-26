using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerOs.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopReportData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredUtc",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceivedBy",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedUtc",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Vehicles",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ReceivedBy",
                table: "WorkOrders",
                column: "ReceivedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ReceivedBy",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Vehicles");
        }
    }
}
