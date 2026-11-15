using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class AddAppNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.AppNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppNotifications] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [RecipientUserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Category] nvarchar(50) NOT NULL CONSTRAINT [DF_AppNotifications_Category] DEFAULT N'General',
        [Severity] nvarchar(20) NOT NULL CONSTRAINT [DF_AppNotifications_Severity] DEFAULT N'info',
        [ReferenceEntity] nvarchar(100) NULL,
        [ReferenceId] int NULL,
        [ActionUrl] nvarchar(500) NULL,
        [IsRead] bit NOT NULL CONSTRAINT [DF_AppNotifications_IsRead] DEFAULT 0,
        [ReadAt] datetime2 NULL,
        [TenantId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedById] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_AppNotifications_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedById] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AppNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AppNotifications_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AppNotifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AppNotifications_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AppNotifications_Users_ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_AppNotifications_TenantId_RecipientUserId_IsRead_CreatedAt]
        ON [dbo].[AppNotifications] ([TenantId], [RecipientUserId], [IsRead], [CreatedAt]);
    CREATE INDEX [IX_AppNotifications_RecipientUserId] ON [dbo].[AppNotifications] ([RecipientUserId]);
    CREATE INDEX [IX_AppNotifications_CreatedById] ON [dbo].[AppNotifications] ([CreatedById]);
    CREATE INDEX [IX_AppNotifications_ModifiedById] ON [dbo].[AppNotifications] ([ModifiedById]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.AppNotifications', N'U') IS NOT NULL
    DROP TABLE [dbo].[AppNotifications];
");
        }
    }
}
