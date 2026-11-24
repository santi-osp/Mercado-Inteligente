using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DataLifecycleAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "SyncRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Trigger",
                table: "SyncRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BackupDailyRetention",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BackupMonthlyRetention",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BackupWeeklyRetention",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LogRetentionDays",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PromoTtlHours",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RawRetentionDays",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "ExternalProducts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MissingCount",
                table: "ExternalProducts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ExternalProducts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Context",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "BackupDailyRetention",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "BackupMonthlyRetention",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "BackupWeeklyRetention",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LogRetentionDays",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PromoTtlHours",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "RawRetentionDays",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "ExternalProducts");

            migrationBuilder.DropColumn(
                name: "MissingCount",
                table: "ExternalProducts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ExternalProducts");
        }
    }
}
