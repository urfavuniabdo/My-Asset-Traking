using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracking.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Locations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentLocationId",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ParentLocationId",
                table: "Locations");
        }
    }
}
