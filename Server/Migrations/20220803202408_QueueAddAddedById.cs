using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sharenima.Server.Migrations
{
    public partial class QueueAddAddedById : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddedById",
                table: "Queues",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedById",
                table: "Queues");
        }
    }
}
