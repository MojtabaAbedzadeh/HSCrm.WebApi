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
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Tenants', N'DefaultCostingMethod') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [DefaultCostingMethod] tinyint NOT NULL CONSTRAINT [DF_Tenants_DefaultCostingMethod] DEFAULT CAST(1 AS tinyint);

IF COL_LENGTH(N'dbo.Tenants', N'DefaultVatPercent') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [DefaultVatPercent] decimal(18,2) NOT NULL CONSTRAINT [DF_Tenants_DefaultVatPercent] DEFAULT 0.0;

IF COL_LENGTH(N'dbo.Tenants', N'DocumentLockDate') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [DocumentLockDate] datetime2 NULL;

IF COL_LENGTH(N'dbo.Tenants', N'InvoiceFooterText') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [InvoiceFooterText] nvarchar(max) NULL;

IF COL_LENGTH(N'dbo.Tenants', N'InvoiceNumberingMode') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [InvoiceNumberingMode] nvarchar(20) NOT NULL CONSTRAINT [DF_Tenants_InvoiceNumberingMode] DEFAULT N'Hybrid';

IF COL_LENGTH(N'dbo.Tenants', N'PurchaseInvoicePattern') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [PurchaseInvoicePattern] nvarchar(max) NOT NULL CONSTRAINT [DF_Tenants_PurchaseInvoicePattern] DEFAULT N'PINV-{YEAR}-{SEQ:4}';

IF COL_LENGTH(N'dbo.Tenants', N'PurchaseReturnPattern') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [PurchaseReturnPattern] nvarchar(max) NOT NULL CONSTRAINT [DF_Tenants_PurchaseReturnPattern] DEFAULT N'PRI-{YEAR}-{SEQ:4}';

IF COL_LENGTH(N'dbo.Tenants', N'SalesInvoicePattern') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [SalesInvoicePattern] nvarchar(max) NOT NULL CONSTRAINT [DF_Tenants_SalesInvoicePattern] DEFAULT N'INV-{YEAR}-{SEQ:4}';

IF COL_LENGTH(N'dbo.Tenants', N'SalesReturnPattern') IS NULL
    ALTER TABLE [dbo].[Tenants] ADD [SalesReturnPattern] nvarchar(max) NOT NULL CONSTRAINT [DF_Tenants_SalesReturnPattern] DEFAULT N'SRI-{YEAR}-{SEQ:4}';
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [dbo].[Tenants] WHERE [Id] = 1)
BEGIN
    UPDATE [dbo].[Tenants]
    SET
        [DefaultCostingMethod] = CASE WHEN [DefaultCostingMethod] = 0 THEN CAST(1 AS tinyint) ELSE [DefaultCostingMethod] END,
        [InvoiceNumberingMode] = COALESCE(NULLIF(LTRIM(RTRIM([InvoiceNumberingMode])), N''), N'Hybrid'),
        [PurchaseInvoicePattern] = COALESCE(NULLIF(LTRIM(RTRIM([PurchaseInvoicePattern])), N''), N'PINV-{YEAR}-{SEQ:4}'),
        [PurchaseReturnPattern] = COALESCE(NULLIF(LTRIM(RTRIM([PurchaseReturnPattern])), N''), N'PRI-{YEAR}-{SEQ:4}'),
        [SalesInvoicePattern] = COALESCE(NULLIF(LTRIM(RTRIM([SalesInvoicePattern])), N''), N'INV-{YEAR}-{SEQ:4}'),
        [SalesReturnPattern] = COALESCE(NULLIF(LTRIM(RTRIM([SalesReturnPattern])), N''), N'SRI-{YEAR}-{SEQ:4}')
    WHERE [Id] = 1;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @dropSql nvarchar(max) = N'';
SELECT @dropSql += N'ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Tenants')
  AND c.name IN (
      N'DefaultCostingMethod', N'DefaultVatPercent', N'DocumentLockDate',
      N'InvoiceFooterText', N'InvoiceNumberingMode',
      N'PurchaseInvoicePattern', N'PurchaseReturnPattern',
      N'SalesInvoicePattern', N'SalesReturnPattern'
  );
IF LEN(@dropSql) > 0 EXEC sp_executesql @dropSql;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Tenants', N'DefaultCostingMethod') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [DefaultCostingMethod];
IF COL_LENGTH(N'dbo.Tenants', N'DefaultVatPercent') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [DefaultVatPercent];
IF COL_LENGTH(N'dbo.Tenants', N'DocumentLockDate') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [DocumentLockDate];
IF COL_LENGTH(N'dbo.Tenants', N'InvoiceFooterText') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [InvoiceFooterText];
IF COL_LENGTH(N'dbo.Tenants', N'InvoiceNumberingMode') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [InvoiceNumberingMode];
IF COL_LENGTH(N'dbo.Tenants', N'PurchaseInvoicePattern') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [PurchaseInvoicePattern];
IF COL_LENGTH(N'dbo.Tenants', N'PurchaseReturnPattern') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [PurchaseReturnPattern];
IF COL_LENGTH(N'dbo.Tenants', N'SalesInvoicePattern') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [SalesInvoicePattern];
IF COL_LENGTH(N'dbo.Tenants', N'SalesReturnPattern') IS NOT NULL ALTER TABLE [dbo].[Tenants] DROP COLUMN [SalesReturnPattern];
");
        }
    }
}
