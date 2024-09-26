using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sharenima.Server.Migrations
{
    public partial class QueueAddVideoType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VideoType",
                table: "Queues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoType",
                table: "Queues");
        }
    }
}
