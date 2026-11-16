using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePrintTemplateToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Tenants', N'InvoicePrintTemplateJson') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [InvoicePrintTemplateJson] nvarchar(max) NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Tenants', N'InvoicePrintTemplateJson') IS NOT NULL
    ALTER TABLE [dbo].[Tenants] DROP COLUMN [InvoicePrintTemplateJson];
");
        }
    }
}
