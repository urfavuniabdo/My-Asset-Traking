using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracking.Infrastructure.Migrations
{
    /// <summary>
    /// حقول سجل أعطال المصنع: الفئة الفنية للعطل، خط الإنتاج، زمن التوقف
    /// (محسوب من لحظة التوقف ولحظة استئناف التشغيل)، مَن قام بالحل،
    /// ومرجع الصور قبل/بعد.
    /// </summary>
    public partial class AddFactoryMaintenanceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── الفئة الفنية للعطل ──────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "MaintenanceTickets",
                nullable: false,
                defaultValue: 8); // IssueCategory.Other

            // ── خط الإنتاج / المنطقة ────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "ProductionLineId",
                table: "MaintenanceTickets",
                nullable: true);

            // ── زمن التوقف ──────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedAt",
                table: "MaintenanceTickets",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RestartedAt",
                table: "MaintenanceTickets",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DowntimeMinutes",
                table: "MaintenanceTickets",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CausedProductionStop",
                table: "MaintenanceTickets",
                nullable: false,
                defaultValue: false);

            // ── الحل والتوثيق ───────────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "SolvedByRole",
                table: "MaintenanceTickets",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotosReference",
                table: "MaintenanceTickets",
                maxLength: 500,
                nullable: true);

            // ── مرحلة الصورة المرفقة (قبل/بعد) ──────────────────
            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "Attachments",
                nullable: false,
                defaultValue: 3); // PhotoStage.Other

            // ── الفهارس ─────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_Category",
                table: "MaintenanceTickets",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_ProductionLineId",
                table: "MaintenanceTickets",
                column: "ProductionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_CompanyId_StoppedAt",
                table: "MaintenanceTickets",
                columns: new[] { "CompanyId", "StoppedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceTickets_Locations_ProductionLineId",
                table: "MaintenanceTickets",
                column: "ProductionLineId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceTickets_Locations_ProductionLineId",
                table: "MaintenanceTickets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTickets_CompanyId_StoppedAt",
                table: "MaintenanceTickets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTickets_ProductionLineId",
                table: "MaintenanceTickets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTickets_Category",
                table: "MaintenanceTickets");

            migrationBuilder.DropColumn(name: "Stage", table: "Attachments");
            migrationBuilder.DropColumn(name: "PhotosReference", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "SolvedByRole", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "CausedProductionStop", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "DowntimeMinutes", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "RestartedAt", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "StoppedAt", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "ProductionLineId", table: "MaintenanceTickets");
            migrationBuilder.DropColumn(name: "Category", table: "MaintenanceTickets");
        }
    }
}
