using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySettingsToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DefaultCostingMethod",
                table: "Tenants",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultVatPercent",
                table: "Tenants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentLockDate",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceFooterText",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumberingMode",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Hybrid");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseInvoicePattern",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "PINV-{YEAR}-{SEQ:4}");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseReturnPattern",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "PRI-{YEAR}-{SEQ:4}");

            migrationBuilder.AddColumn<string>(
                name: "SalesInvoicePattern",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "INV-{YEAR}-{SEQ:4}");

            migrationBuilder.AddColumn<string>(
                name: "SalesReturnPattern",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "SRI-{YEAR}-{SEQ:4}");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[]
                {
                    "DefaultCostingMethod",
                    "DefaultVatPercent",
                    "InvoiceNumberingMode",
                    "PurchaseInvoicePattern",
                    "PurchaseReturnPattern",
                    "SalesInvoicePattern",
                    "SalesReturnPattern"
                },
                values: new object[]
                {
                    (byte)1,
                    0m,
                    "Hybrid",
                    "PINV-{YEAR}-{SEQ:4}",
                    "PRI-{YEAR}-{SEQ:4}",
                    "INV-{YEAR}-{SEQ:4}",
                    "SRI-{YEAR}-{SEQ:4}"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCostingMethod",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DefaultVatPercent",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DocumentLockDate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InvoiceFooterText",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InvoiceNumberingMode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoicePattern",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PurchaseReturnPattern",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SalesInvoicePattern",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SalesReturnPattern",
                table: "Tenants");
        }
    }
}
