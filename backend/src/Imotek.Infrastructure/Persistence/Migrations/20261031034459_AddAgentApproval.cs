using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imotek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentRejectionReason",
                table: "AspNetUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentRequestMessage",
                table: "AspNetUsers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgentRequestedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgentReviewedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentStatus",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<bool>(
                name: "PhonePublicConsent",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AgentStatus",
                table: "AspNetUsers",
                column: "AgentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AgentStatus",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentRejectionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentRequestMessage",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentRequestedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentReviewedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentStatus",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PhonePublicConsent",
                table: "AspNetUsers");
        }
    }
}
