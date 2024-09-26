using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sharenima.Server.Migrations
{
    public partial class QueueAddMediaType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Queues",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Queues");
        }
    }
}
