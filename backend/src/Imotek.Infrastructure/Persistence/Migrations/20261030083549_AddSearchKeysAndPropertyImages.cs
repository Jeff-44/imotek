using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imotek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchKeysAndPropertyImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentKey",
                table: "Properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocationKey",
                table: "Properties",
                type: "character varying(452)",
                maxLength: 452,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchKey",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill existing rows. Mirrors Domain.Common.SearchText for the accents used in
            // French and Haitian Creole; the application recomputes the keys on every update.
            const string accented = "àâäáãåçéèêëíìîïñóòôöõúùûüýÿ’";
            const string plain = "aaaaaaceeeeiiiinooooouuuuyy''"; // '' is an escaped quote in SQL
            static string Key(string column) =>
                $"translate(lower(coalesce(\"{column}\", '')), '{accented}', '{plain}')";
            migrationBuilder.Sql($"""
                UPDATE "Properties" SET
                    "SearchKey" = trim({Key("Title")} || ' ' || {Key("Description")}),
                    "LocationKey" = trim({Key("City")} || ' ' || {Key("Department")} || ' ' || {Key("Address")}),
                    "DepartmentKey" = {Key("Department")};
                """);

            migrationBuilder.CreateTable(
                name: "PropertyImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyImages_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_DepartmentKey",
                table: "Properties",
                column: "DepartmentKey");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_PropertyId",
                table: "PropertyImages",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_StorageKey",
                table: "PropertyImages",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyImages");

            migrationBuilder.DropIndex(
                name: "IX_Properties_DepartmentKey",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DepartmentKey",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LocationKey",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SearchKey",
                table: "Properties");
        }
    }
}
