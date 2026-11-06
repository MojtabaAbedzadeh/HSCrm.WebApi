using HSCrm.DataModel.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCrm.DataModel.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261106110000_ActivateCentralTenant")]
    public partial class ActivateCentralTenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Tenants] SET [IsActive] = 1 WHERE [Id] = 1 AND [TenantCode] = N'Central';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Tenants] SET [IsActive] = 0 WHERE [Id] = 1 AND [TenantCode] = N'Central';");
        }
    }
}