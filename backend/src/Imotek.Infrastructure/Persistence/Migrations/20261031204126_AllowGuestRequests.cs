using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imotek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowGuestRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "VisitRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessTokenHash",
                table: "VisitRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "VisitRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "VisitRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "VisitRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "RentalRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CurrentAddress",
                table: "RentalRequests",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                table: "RentalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessTokenHash",
                table: "RentalRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "RentalRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "RentalRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "RentalRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MoveInDate",
                table: "RentalRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Occupants",
                table: "RentalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "PurchaseRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessTokenHash",
                table: "PurchaseRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "PurchaseRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "PurchaseRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "PurchaseRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "VisitRequests",
                columns: new[] { "PropertyId", "GuestEmail" },
                unique: true,
                filter: "\"ClientId\" IS NULL AND \"Status\" IN ('Pending', 'Responded', 'Accepted', 'Rescheduled')");

            migrationBuilder.CreateIndex(
                name: "IX_VisitRequests_GuestAccessTokenHash",
                table: "VisitRequests",
                column: "GuestAccessTokenHash",
                unique: true,
                filter: "\"GuestAccessTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RentalRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "RentalRequests",
                columns: new[] { "PropertyId", "GuestEmail" },
                unique: true,
                filter: "\"ClientId\" IS NULL AND \"Status\" IN ('Pending', 'Responded', 'Accepted', 'Rescheduled')");

            migrationBuilder.CreateIndex(
                name: "IX_RentalRequests_GuestAccessTokenHash",
                table: "RentalRequests",
                column: "GuestAccessTokenHash",
                unique: true,
                filter: "\"GuestAccessTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "PurchaseRequests",
                columns: new[] { "PropertyId", "GuestEmail" },
                unique: true,
                filter: "\"ClientId\" IS NULL AND \"Status\" IN ('Pending', 'Responded', 'Accepted', 'Rescheduled')");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_GuestAccessTokenHash",
                table: "PurchaseRequests",
                column: "GuestAccessTokenHash",
                unique: true,
                filter: "\"GuestAccessTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "VisitRequests");

            migrationBuilder.DropIndex(
                name: "IX_VisitRequests_GuestAccessTokenHash",
                table: "VisitRequests");

            migrationBuilder.DropIndex(
                name: "IX_RentalRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "RentalRequests");

            migrationBuilder.DropIndex(
                name: "IX_RentalRequests_GuestAccessTokenHash",
                table: "RentalRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_ActiveGuest_PropertyId_GuestEmail",
                table: "PurchaseRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_GuestAccessTokenHash",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "GuestAccessTokenHash",
                table: "VisitRequests");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "VisitRequests");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "VisitRequests");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "VisitRequests");

            migrationBuilder.DropColumn(
                name: "CurrentAddress",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "GuestAccessTokenHash",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "MoveInDate",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "Occupants",
                table: "RentalRequests");

            migrationBuilder.DropColumn(
                name: "GuestAccessTokenHash",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "PurchaseRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "VisitRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "RentalRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "PurchaseRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
