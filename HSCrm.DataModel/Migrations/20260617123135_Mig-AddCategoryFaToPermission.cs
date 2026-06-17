using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class MigAddCategoryFaToPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Permissions",
                newName: "CategoryTitleEn");

            migrationBuilder.AddColumn<string>(
                name: "CategoryTitleFa",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "FiscalYears",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 17, 12, 31, 33, 261, DateTimeKind.Utc).AddTicks(9649));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryTitleFa",
                table: "Permissions");

            migrationBuilder.RenameColumn(
                name: "CategoryTitleEn",
                table: "Permissions",
                newName: "Category");

            migrationBuilder.UpdateData(
                table: "FiscalYears",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 10, 44, 52, 911, DateTimeKind.Utc).AddTicks(4526));
        }
    }
}
