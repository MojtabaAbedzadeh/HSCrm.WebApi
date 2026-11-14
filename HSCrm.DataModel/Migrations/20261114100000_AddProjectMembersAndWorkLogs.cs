using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMembersAndWorkLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ProjectMembers', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectMembers] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProjectId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ProjectRole] nvarchar(100) NULL,
        [HourlyRate] decimal(18,2) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_ProjectMembers_IsActive] DEFAULT 1,
        [JoinedAt] datetime2 NOT NULL,
        [TenantId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedById] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ProjectMembers_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedById] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMembers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMembers_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectMembers_Users_ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_ProjectMembers_TenantId_ProjectId_UserId]
        ON [dbo].[ProjectMembers] ([TenantId], [ProjectId], [UserId])
        WHERE [IsDeleted] = 0;

    CREATE INDEX [IX_ProjectMembers_ProjectId] ON [dbo].[ProjectMembers] ([ProjectId]);
    CREATE INDEX [IX_ProjectMembers_UserId] ON [dbo].[ProjectMembers] ([UserId]);
    CREATE INDEX [IX_ProjectMembers_CreatedById] ON [dbo].[ProjectMembers] ([CreatedById]);
    CREATE INDEX [IX_ProjectMembers_ModifiedById] ON [dbo].[ProjectMembers] ([ModifiedById]);
END

IF OBJECT_ID(N'dbo.ProjectWorkLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectWorkLogs] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProjectId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [FiscalYearId] int NOT NULL,
        [WorkDate] datetime2 NOT NULL,
        [Hours] decimal(18,2) NOT NULL,
        [HourlyRateSnapshot] decimal(18,2) NOT NULL,
        [WageCost] decimal(18,2) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Status] tinyint NOT NULL CONSTRAINT [DF_ProjectWorkLogs_Status] DEFAULT CAST(0 AS tinyint),
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewNote] nvarchar(500) NULL,
        [TenantId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedById] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ProjectWorkLogs_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedById] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_ProjectWorkLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectWorkLogs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [dbo].[FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectWorkLogs_Users_ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_ProjectWorkLogs_TenantId_FiscalYearId_ProjectId_WorkDate]
        ON [dbo].[ProjectWorkLogs] ([TenantId], [FiscalYearId], [ProjectId], [WorkDate]);

    CREATE INDEX [IX_ProjectWorkLogs_TenantId_UserId_Status]
        ON [dbo].[ProjectWorkLogs] ([TenantId], [UserId], [Status]);

    CREATE INDEX [IX_ProjectWorkLogs_ProjectId] ON [dbo].[ProjectWorkLogs] ([ProjectId]);
    CREATE INDEX [IX_ProjectWorkLogs_UserId] ON [dbo].[ProjectWorkLogs] ([UserId]);
    CREATE INDEX [IX_ProjectWorkLogs_FiscalYearId] ON [dbo].[ProjectWorkLogs] ([FiscalYearId]);
    CREATE INDEX [IX_ProjectWorkLogs_ReviewedByUserId] ON [dbo].[ProjectWorkLogs] ([ReviewedByUserId]);
    CREATE INDEX [IX_ProjectWorkLogs_CreatedById] ON [dbo].[ProjectWorkLogs] ([CreatedById]);
    CREATE INDEX [IX_ProjectWorkLogs_ModifiedById] ON [dbo].[ProjectWorkLogs] ([ModifiedById]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ProjectWorkLogs', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectWorkLogs];

IF OBJECT_ID(N'dbo.ProjectMembers', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectMembers];
");
        }
    }
}
