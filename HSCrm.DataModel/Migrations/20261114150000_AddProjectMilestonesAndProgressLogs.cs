using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMilestonesAndProgressLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Projects', N'EstimatedBudget') IS NULL
BEGIN
    ALTER TABLE [dbo].[Projects] ADD [EstimatedBudget] decimal(18,2) NULL;
END

IF COL_LENGTH(N'dbo.Projects', N'ProgressPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[Projects] ADD [ProgressPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_Projects_ProgressPercent] DEFAULT 0;
END

IF OBJECT_ID(N'dbo.ProjectMilestones', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectMilestones] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProjectId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [SortOrder] int NOT NULL CONSTRAINT [DF_ProjectMilestones_SortOrder] DEFAULT 0,
        [WeightPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_ProjectMilestones_WeightPercent] DEFAULT 0,
        [ProgressPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_ProjectMilestones_ProgressPercent] DEFAULT 0,
        [DueDate] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Status] tinyint NOT NULL CONSTRAINT [DF_ProjectMilestones_Status] DEFAULT CAST(0 AS tinyint),
        [TenantId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedById] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ProjectMilestones_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedById] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectMilestones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectMilestones_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMilestones_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMilestones_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMilestones_Users_ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_ProjectMilestones_TenantId_ProjectId_SortOrder]
        ON [dbo].[ProjectMilestones] ([TenantId], [ProjectId], [SortOrder]);
    CREATE INDEX [IX_ProjectMilestones_ProjectId] ON [dbo].[ProjectMilestones] ([ProjectId]);
    CREATE INDEX [IX_ProjectMilestones_CreatedById] ON [dbo].[ProjectMilestones] ([CreatedById]);
    CREATE INDEX [IX_ProjectMilestones_ModifiedById] ON [dbo].[ProjectMilestones] ([ModifiedById]);
END

IF OBJECT_ID(N'dbo.ProjectProgressLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectProgressLogs] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProjectId] int NOT NULL,
        [MilestoneId] int NULL,
        [FiscalYearId] int NOT NULL,
        [RecordedByUserId] nvarchar(450) NOT NULL,
        [LogDate] datetime2 NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [ReportText] nvarchar(2000) NOT NULL,
        [ProgressPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_ProjectProgressLogs_ProgressPercent] DEFAULT 0,
        [WorkshopArea] nvarchar(300) NULL,
        [TenantId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedById] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ProjectProgressLogs_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedById] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectProgressLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectProgressLogs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_ProjectMilestones_MilestoneId] FOREIGN KEY ([MilestoneId]) REFERENCES [dbo].[ProjectMilestones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [dbo].[FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_Users_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectProgressLogs_Users_ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_ProjectProgressLogs_TenantId_ProjectId_LogDate]
        ON [dbo].[ProjectProgressLogs] ([TenantId], [ProjectId], [LogDate]);
    CREATE INDEX [IX_ProjectProgressLogs_ProjectId] ON [dbo].[ProjectProgressLogs] ([ProjectId]);
    CREATE INDEX [IX_ProjectProgressLogs_MilestoneId] ON [dbo].[ProjectProgressLogs] ([MilestoneId]);
    CREATE INDEX [IX_ProjectProgressLogs_FiscalYearId] ON [dbo].[ProjectProgressLogs] ([FiscalYearId]);
    CREATE INDEX [IX_ProjectProgressLogs_RecordedByUserId] ON [dbo].[ProjectProgressLogs] ([RecordedByUserId]);
    CREATE INDEX [IX_ProjectProgressLogs_CreatedById] ON [dbo].[ProjectProgressLogs] ([CreatedById]);
    CREATE INDEX [IX_ProjectProgressLogs_ModifiedById] ON [dbo].[ProjectProgressLogs] ([ModifiedById]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ProjectProgressLogs', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectProgressLogs];

IF OBJECT_ID(N'dbo.ProjectMilestones', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectMilestones];

IF COL_LENGTH(N'dbo.Projects', N'ProgressPercent') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.DF_Projects_ProgressPercent', N'D') IS NOT NULL
        ALTER TABLE [dbo].[Projects] DROP CONSTRAINT [DF_Projects_ProgressPercent];
    ALTER TABLE [dbo].[Projects] DROP COLUMN [ProgressPercent];
END

IF COL_LENGTH(N'dbo.Projects', N'EstimatedBudget') IS NOT NULL
    ALTER TABLE [dbo].[Projects] DROP COLUMN [EstimatedBudget];
");
        }
    }
}
