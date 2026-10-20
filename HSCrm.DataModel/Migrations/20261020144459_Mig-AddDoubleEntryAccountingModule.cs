using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class MigAddDoubleEntryAccountingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Nature = table.Column<byte>(type: "tinyint", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingAccounts_AccountingAccounts_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AccountingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingAccounts_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingAccounts_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    VoucherNumber = table.Column<int>(type: "int", nullable: false),
                    VoucherDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReferenceType = table.Column<byte>(type: "tinyint", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalVouchers_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVouchers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVouchers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVouchers_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalVoucherItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalVoucherId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalVoucherItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_AccountingAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AccountingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_JournalVouchers_JournalVoucherId",
                        column: x => x.JournalVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalVoucherItems_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: "28dfc395-0464-4b47-8aae-c73953a9d153",
                column: "UserType",
                value: (byte)110);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_CreatedById",
                table: "AccountingAccounts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_ModifiedById",
                table: "AccountingAccounts",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_ParentId",
                table: "AccountingAccounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_TenantId_Code",
                table: "AccountingAccounts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_AccountId",
                table: "JournalVoucherItems",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_CreatedById",
                table: "JournalVoucherItems",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_CustomerId",
                table: "JournalVoucherItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_JournalVoucherId",
                table: "JournalVoucherItems",
                column: "JournalVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_ModifiedById",
                table: "JournalVoucherItems",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_ProjectId",
                table: "JournalVoucherItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_SupplierId",
                table: "JournalVoucherItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_TenantId",
                table: "JournalVoucherItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherItems_WarehouseId",
                table: "JournalVoucherItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_CreatedById",
                table: "JournalVouchers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_FiscalYearId",
                table: "JournalVouchers",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_ModifiedById",
                table: "JournalVouchers",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_TenantId_FiscalYearId_VoucherNumber",
                table: "JournalVouchers",
                columns: new[] { "TenantId", "FiscalYearId", "VoucherNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalVoucherItems");

            migrationBuilder.DropTable(
                name: "AccountingAccounts");

            migrationBuilder.DropTable(
                name: "JournalVouchers");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: "28dfc395-0464-4b47-8aae-c73953a9d153",
                column: "UserType",
                value: (byte)1);
        }
    }
}
