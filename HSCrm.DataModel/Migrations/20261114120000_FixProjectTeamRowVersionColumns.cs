using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class FixProjectTeamRowVersionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.ProjectMembers')
      AND c.name = N'RowVersion'
      AND t.name <> N'timestamp'
)
BEGIN
    ALTER TABLE [dbo].[ProjectMembers] DROP COLUMN [RowVersion];
    ALTER TABLE [dbo].[ProjectMembers] ADD [RowVersion] rowversion NOT NULL;
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.ProjectWorkLogs')
      AND c.name = N'RowVersion'
      AND t.name <> N'timestamp'
)
BEGIN
    ALTER TABLE [dbo].[ProjectWorkLogs] DROP COLUMN [RowVersion];
    ALTER TABLE [dbo].[ProjectWorkLogs] ADD [RowVersion] rowversion NOT NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: rowversion is required by EF Core concurrency token
        }
    }
}
