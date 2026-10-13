using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracking.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class WarehouseSiteCompositeOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_CompanySites_CompanySiteId",
                table: "Warehouses");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CompanySites_Id_CompanyId",
                table: "CompanySites",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CompanySiteId_CompanyId",
                table: "Warehouses",
                columns: new[] { "CompanySiteId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySites_Id_CompanyId",
                table: "CompanySites",
                columns: new[] { "Id", "CompanyId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_CompanySites_CompanySiteId_CompanyId",
                table: "Warehouses",
                columns: new[] { "CompanySiteId", "CompanyId" },
                principalTable: "CompanySites",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_CompanySites_CompanySiteId_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CompanySiteId_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CompanySites_Id_CompanyId",
                table: "CompanySites");

            migrationBuilder.DropIndex(
                name: "IX_CompanySites_Id_CompanyId",
                table: "CompanySites");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_CompanySites_CompanySiteId",
                table: "Warehouses",
                column: "CompanySiteId",
                principalTable: "CompanySites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
