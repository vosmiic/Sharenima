using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sharenima.Server.Migrations
{
    public partial class InstancePermissionsTableAnonymousUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnonymousUser",
                table: "InstancePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymousUser",
                table: "InstancePermissions");
        }
    }
}
