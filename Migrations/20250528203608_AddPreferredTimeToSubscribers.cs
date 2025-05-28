using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotivationQuotesApi.Migrations
{
    public partial class AddPreferredTimeToSubscribers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "PreferredTime",
                table: "DailySubscribers",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredTime",
                table: "DailySubscribers");
        }
    }
}
