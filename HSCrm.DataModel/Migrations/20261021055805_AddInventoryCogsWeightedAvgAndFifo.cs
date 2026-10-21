using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCogsWeightedAvgAndFifo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "SalesInvoiceItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SalesInvoiceItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingFifoQuantity",
                table: "InventoryTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "InventoryTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InventoryTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageUnitCost",
                table: "Inventories",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "RemainingFifoQuantity",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "AverageUnitCost",
                table: "Inventories");
        }
    }
}
