using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceReferencesToCheque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Cheques', N'SalesInvoiceId') IS NULL
    ALTER TABLE [dbo].[Cheques] ADD [SalesInvoiceId] int NULL;
IF COL_LENGTH(N'dbo.Cheques', N'PurchaseInvoiceId') IS NULL
    ALTER TABLE [dbo].[Cheques] ADD [PurchaseInvoiceId] int NULL;
IF COL_LENGTH(N'dbo.Cheques', N'ExpenseId') IS NULL
    ALTER TABLE [dbo].[Cheques] ADD [ExpenseId] int NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Cheques', N'SalesInvoiceId') IS NOT NULL
    ALTER TABLE [dbo].[Cheques] DROP COLUMN [SalesInvoiceId];
IF COL_LENGTH(N'dbo.Cheques', N'PurchaseInvoiceId') IS NOT NULL
    ALTER TABLE [dbo].[Cheques] DROP COLUMN [PurchaseInvoiceId];
IF COL_LENGTH(N'dbo.Cheques', N'ExpenseId') IS NOT NULL
    ALTER TABLE [dbo].[Cheques] DROP COLUMN [ExpenseId];
");
        }
    }
}
