using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sharenima.Server.Migrations
{
    public partial class InstanceAddVideoTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "VideoTime",
                table: "Instances",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoTime",
                table: "Instances");
        }
    }
}
