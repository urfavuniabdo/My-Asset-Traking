using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AssetTracking.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SplitPlacesWarehouseSiteLine : Migration
    {
        // ══════════════════════════════════════════════════════════════════
        // لماذا كُتب هذا الترحيل يدويًا رغم أن EF ولّد واحدًا؟
        //
        // رأى EF عمودًا قديمًا («LocationId») وعمودًا جديدًا («WarehouseId»)
        // فسّمّاهما إعادة تسمية. إعادة التسمية كذبة بيانات: العمود القديم كان
        // يحمل معرّفًا من جدول واحد بثلاثة معانٍ — مخزن رقم ٥ لا يُفرَّق فيه عن
        // فرع رقم ٥ عن خط رقم ٥. فالصحيح: أعمدة جديدة، ثم نسخة صفوف «Locations»
        // حسب نوع كل سجل، ثم توجيه كل سطر بقراءة نوع سجله، ثم إسقاط القديم.
        //
        // الجسر «LegacyLocationId» المؤقّت هو وحده ما يربط المعرّف الجديد
        // بالقديم: لا افتراض لترتيب الصفوف ولا مقارنة أسماء. يُسقط في النهاية
        // فالنموذج لا يعرفه.
        //
        // الأنواع: 3 = مخزن، 8 = خط إنتاج، وما عداهما موقع شركة (أرقام SiteKind
        // مطابقة عمدًا لـ LocationType في الحدود المشتركة، فنسخة بلا إعادة ترقيم).
        //
        // ما لا عمود له في الكيان الجديد (جهة اتصال/إحداثيات/مدينة لمخزن أو خط)
        // يُطوى في «Notes» بعلامة «[ترحيل]» صريحة، ولا يُحذف في هذا الترحيل.
        // ══════════════════════════════════════════════════════════════════

        private const string AuditCols =
            "\"CreatedAt\", \"CreatedBy\", \"UpdatedAt\", \"UpdatedBy\", \"IsDeleted\", \"DeletedAt\", \"DeletedBy\"";

        private const string AuditFromL =
            "l.\"CreatedAt\", l.\"CreatedBy\", l.\"UpdatedAt\", l.\"UpdatedBy\", l.\"IsDeleted\", l.\"DeletedAt\", l.\"DeletedBy\"";

        /// <summary>
        /// عنوان المخزن كان مفرّقًا في المواقع (عنوان/مدينة/محافظة) — يُدمج في
        /// عمود العنوان الوحيد الذي يسمح به الكيان، بلا اختراع أعمدة موقع.
        /// </summary>
        private const string AddressFold =
            "substr(NULLIF(LTRIM(RTRIM(" +
            "COALESCE(l.\"Address\", '') " +
            "|| CASE WHEN COALESCE(l.\"City\", '') = '' THEN '' " +
            "WHEN COALESCE(l.\"Address\", '') = '' THEN l.\"City\" " +
            "ELSE ' - ' || l.\"City\" END " +
            "|| CASE WHEN COALESCE(l.\"Governorate\", '') = '' THEN '' " +
            "WHEN COALESCE(l.\"Address\", '') = '' AND COALESCE(l.\"City\", '') = '' THEN l.\"Governorate\" " +
            "ELSE ' - ' || l.\"Governorate\" END)), ''), 1, 400)";

        /// <summary>
        /// البقايا التي لا عمود لها تصير سطرًا وصفيًا في «Notes». المدينة والمحافظة
        /// تدخلان هنا للخطوط فقط (لا عنوان لكيان خط الإنتاج)، ومخزنٌ تُنقل مدينته
        /// إلى عنوانه فلا يحتاج تكرارها.
        /// </summary>
        private static string NotesFold(bool withPlace)
            => "substr(NULLIF(LTRIM(RTRIM(COALESCE(l.\"Notes\", '') || CASE WHEN " +
               "COALESCE(l.\"ContactPerson\", '') = '' AND COALESCE(l.\"ContactPhone\", '') = '' " +
               (withPlace ? "AND COALESCE(l.\"City\", '') = '' AND COALESCE(l.\"Governorate\", '') = '' " : "") +
               "AND l.\"Latitude\" IS NULL AND l.\"Longitude\" IS NULL THEN '' ELSE ' [ترحيل]'" +
               " || CASE WHEN COALESCE(l.\"ContactPerson\", '') = '' THEN '' ELSE ' | مسؤول: ' || l.\"ContactPerson\" END" +
               " || CASE WHEN COALESCE(l.\"ContactPhone\", '') = '' THEN '' ELSE ' | هاتف: ' || l.\"ContactPhone\" END" +
               (withPlace ? " || CASE WHEN COALESCE(l.\"City\", '') = '' THEN '' ELSE ' | مكان: ' || l.\"City\" END" +
                            " || CASE WHEN COALESCE(l.\"Governorate\", '') = '' THEN '' ELSE ' | محافظة: ' || l.\"Governorate\" END" : "") +
               " || CASE WHEN l.\"Latitude\" IS NULL OR l.\"Longitude\" IS NULL THEN ''" +
               " ELSE ' | إحداثي: ' || CAST(l.\"Latitude\" AS TEXT) || ', ' || CAST(l.\"Longitude\" AS TEXT) END" +
               " END)), ''), 1, 500)";

        /// <summary>
        /// معرّف الموضع القديم يسكن جدولًا واحدًا، فالتوجيه ثلاثة أعمدة يملؤها
        /// البحث عن ذلك المعرّف: عمود بقيمة واثنان بـ null — لا قرعة على النوع.
        /// </summary>
        private static string Repoint(string table, string legacy, string wh, string site, string line)
            => $"UPDATE \"{table}\" SET " +
               $"\"{wh}\" = (SELECT w.\"Id\" FROM \"Warehouses\" w WHERE w.\"LegacyLocationId\" = \"{table}\".\"{legacy}\"), " +
               $"\"{site}\" = (SELECT s.\"Id\" FROM \"CompanySites\" s WHERE s.\"LegacyLocationId\" = \"{table}\".\"{legacy}\"), " +
               $"\"{line}\" = (SELECT p.\"Id\" FROM \"ProductionLines\" p WHERE p.\"LegacyLocationId\" = \"{table}\".\"{legacy}\") " +
               $"WHERE \"{table}\".\"{legacy}\" IS NOT NULL";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ١) تُفكّ المراجع القديمة أولًا: ستُعاد توجيهها لا تُهدر بياناتها ──
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Locations_LocationId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryAudits_Locations_LocationId",
                table: "InventoryAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceTickets_Locations_ProductionLineId",
                table: "MaintenanceTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Assets_LocationId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_InventoryAudits_LocationId",
                table: "InventoryAudits");

            // ── ٢) الكيانات الثلاثة المستقلة، ولكلٍّ جسر مؤقت إلى سجله القديم ──
            migrationBuilder.CreateTable(
                name: "CompanySites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(nullable: false),
                    NameAr = table.Column<string>(maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(maxLength: 200, nullable: true),
                    Code = table.Column<string>(maxLength: 30, nullable: true),
                    Kind = table.Column<int>(nullable: false),
                    Address = table.Column<string>(maxLength: 400, nullable: true),
                    City = table.Column<string>(maxLength: 100, nullable: true),
                    Governorate = table.Column<string>(maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(precision: 10, scale: 7, nullable: true),
                    ContactPerson = table.Column<string>(maxLength: 150, nullable: true),
                    ContactPhone = table.Column<string>(maxLength: 30, nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<string>(nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    UpdatedBy = table.Column<string>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false),
                    DeletedAt = table.Column<DateTime>(nullable: true),
                    DeletedBy = table.Column<string>(nullable: true),
                    LegacyLocationId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySites_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(nullable: false),
                    NameAr = table.Column<string>(maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(maxLength: 200, nullable: true),
                    Code = table.Column<string>(maxLength: 30, nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<string>(nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    UpdatedBy = table.Column<string>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false),
                    DeletedAt = table.Column<DateTime>(nullable: true),
                    DeletedBy = table.Column<string>(nullable: true),
                    LegacyLocationId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // المرجع هنا داخل الجدول نفسه: لا جدول مخازن بلا قرار ملكية الفرع.
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(nullable: false),
                    NameAr = table.Column<string>(maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(maxLength: 200, nullable: true),
                    Code = table.Column<string>(maxLength: 30, nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    Address = table.Column<string>(maxLength: 400, nullable: true),
                    CompanySiteId = table.Column<int>(nullable: true),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<string>(nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    UpdatedBy = table.Column<string>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false),
                    DeletedAt = table.Column<DateTime>(nullable: true),
                    DeletedBy = table.Column<string>(nullable: true),
                    LegacyLocationId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Warehouses_CompanySites_CompanySiteId",
                        column: x => x.CompanySiteId,
                        principalTable: "CompanySites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── ٣) نسخة الصفوف: نوع السجل الواحد يطابق جدولًا واحدًا ──
            // موقع الشركة يأخذ أعمدته حرفيًا — لا شيء يُطوى، فالكيان يحملها كلها.
            migrationBuilder.Sql(
                "INSERT INTO \"CompanySites\" (\"CompanyId\", \"NameAr\", \"NameEn\", \"Code\", \"Kind\", " +
                "\"Address\", \"City\", \"Governorate\", \"Latitude\", \"Longitude\", \"ContactPerson\", " +
                "\"ContactPhone\", \"Notes\", \"IsActive\", " + AuditCols + ", \"LegacyLocationId\") " +
                "SELECT l.\"CompanyId\", l.\"NameAr\", l.\"NameEn\", l.\"Code\", l.\"Type\", l.\"Address\", " +
                "l.\"City\", l.\"Governorate\", l.\"Latitude\", l.\"Longitude\", l.\"ContactPerson\", " +
                "l.\"ContactPhone\", l.\"Notes\", l.\"IsActive\", " + AuditFromL + ", l.\"Id\" " +
                "FROM \"Locations\" l WHERE l.\"Type\" <> 3 AND l.\"Type\" <> 8 ORDER BY l.\"Id\"");

            migrationBuilder.Sql(
                "INSERT INTO \"ProductionLines\" (\"CompanyId\", \"NameAr\", \"NameEn\", \"Code\", \"Notes\", " +
                "\"IsActive\", " + AuditCols + ", \"LegacyLocationId\") " +
                "SELECT l.\"CompanyId\", l.\"NameAr\", l.\"NameEn\", l.\"Code\", " + NotesFold(true) + ", " +
                "l.\"IsActive\", " + AuditFromL + ", l.\"Id\" " +
                "FROM \"Locations\" l WHERE l.\"Type\" = 8 ORDER BY l.\"Id\"");

            // المخزن يبدأ بلا فرع: null حالة كاملة لا نقص، ثم يُربط في الخطوة ٤.
            migrationBuilder.Sql(
                "INSERT INTO \"Warehouses\" (\"CompanyId\", \"NameAr\", \"NameEn\", \"Code\", \"Notes\", " +
                "\"Address\", \"CompanySiteId\", \"IsActive\", " + AuditCols + ", \"LegacyLocationId\") " +
                "SELECT l.\"CompanyId\", l.\"NameAr\", l.\"NameEn\", l.\"Code\", " + NotesFold(false) + ", " +
                AddressFold + ", NULL, l.\"IsActive\", " + AuditFromL + ", l.\"Id\" " +
                "FROM \"Locations\" l WHERE l.\"Type\" = 3 ORDER BY l.\"Id\"");

            // ── ٤) سلّم المواقع القديم كان يجيب عن «أي فرع هذا؟» للمخزن ──
            // أبٌ ليس موقعَ شركة (تسلسل نادِر) يترك المخزن مستقلًا: لا اختراع
            // لعلاقة لا وجود لها في النموذج الجديد.
            migrationBuilder.Sql(
                "UPDATE \"Warehouses\" SET \"CompanySiteId\" = " +
                "(SELECT s.\"Id\" FROM \"CompanySites\" s WHERE s.\"LegacyLocationId\" = " +
                "(SELECT l.\"ParentLocationId\" FROM \"Locations\" l WHERE l.\"Id\" = \"Warehouses\".\"LegacyLocationId\")) " +
                "WHERE \"LegacyLocationId\" IS NOT NULL");

            // ── ٥) أعمدة الموضع الجديدة لدى المستهلكين ──
            migrationBuilder.AddColumn<int>(name: "WarehouseId", table: "Assets", nullable: true);
            migrationBuilder.AddColumn<int>(name: "CompanySiteId", table: "Assets", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ProductionLineId", table: "Assets", nullable: true);

            migrationBuilder.AddColumn<int>(name: "WarehouseId", table: "InventoryAudits", nullable: true);
            migrationBuilder.AddColumn<int>(name: "CompanySiteId", table: "InventoryAudits", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ProductionLineId", table: "InventoryAudits", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ExpectedWarehouseId", table: "InventoryAuditItems", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ExpectedSiteId", table: "InventoryAuditItems", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ExpectedLineId", table: "InventoryAuditItems", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActualWarehouseId", table: "InventoryAuditItems", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActualSiteId", table: "InventoryAuditItems", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActualLineId", table: "InventoryAuditItems", nullable: true);

            // النقلة تاريخ، فتبقى أرقامًا بلا مراجع: حذف سجل مرجعي لا يمحو ما جرى.
            migrationBuilder.AddColumn<int>(name: "FromWarehouseId", table: "LocationLogs", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FromSiteId", table: "LocationLogs", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FromLineId", table: "LocationLogs", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ToWarehouseId", table: "LocationLogs", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ToSiteId", table: "LocationLogs", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ToLineId", table: "LocationLogs", nullable: true);

            // ── ٦) التوجيه: نوع السجل الأصلي يُقرأ، لا يُخمَّن من رقم ──
            migrationBuilder.Sql(Repoint("Assets", "LocationId",
                "WarehouseId", "CompanySiteId", "ProductionLineId"));

            migrationBuilder.Sql(Repoint("InventoryAudits", "LocationId",
                "WarehouseId", "CompanySiteId", "ProductionLineId"));

            migrationBuilder.Sql(Repoint("InventoryAuditItems", "ExpectedLocationId",
                "ExpectedWarehouseId", "ExpectedSiteId", "ExpectedLineId"));

            migrationBuilder.Sql(Repoint("InventoryAuditItems", "ActualLocationId",
                "ActualWarehouseId", "ActualSiteId", "ActualLineId"));

            // تذكرة الصيانة كانت تشير «الخط» داخل Locations؛ ما ليس خطًا (كموقع
            // مشروع) لا صار خطًا ولا اختير له بديل — يرجع بلا مرجع صريحًا.
            migrationBuilder.Sql(
                "UPDATE \"MaintenanceTickets\" SET \"ProductionLineId\" = " +
                "(SELECT p.\"Id\" FROM \"ProductionLines\" p " +
                "WHERE p.\"LegacyLocationId\" = \"MaintenanceTickets\".\"ProductionLineId\") " +
                "WHERE \"ProductionLineId\" IS NOT NULL");

            migrationBuilder.Sql(Repoint("LocationLogs", "FromLocationId",
                "FromWarehouseId", "FromSiteId", "FromLineId"));

            migrationBuilder.Sql(Repoint("LocationLogs", "ToLocationId",
                "ToWarehouseId", "ToSiteId", "ToLineId"));

            // ── ٧) العمود القديم غُنِيَ عن وظيفته: يُسقط لا يُعاد تفسيره ──
            migrationBuilder.DropColumn(name: "LocationId", table: "Assets");
            migrationBuilder.DropColumn(name: "LocationId", table: "InventoryAudits");
            migrationBuilder.DropColumn(name: "ExpectedLocationId", table: "InventoryAuditItems");
            migrationBuilder.DropColumn(name: "ActualLocationId", table: "InventoryAuditItems");
            migrationBuilder.DropColumn(name: "FromLocationId", table: "LocationLogs");
            migrationBuilder.DropColumn(name: "ToLocationId", table: "LocationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropColumn(name: "ParentLocationId", table: "Locations");

            // ── ٨) الجسر يُهدم بعد أن عبره الجميع ──
            migrationBuilder.DropColumn(name: "LegacyLocationId", table: "CompanySites");
            migrationBuilder.DropColumn(name: "LegacyLocationId", table: "ProductionLines");
            migrationBuilder.DropColumn(name: "LegacyLocationId", table: "Warehouses");

            // ── ٩) قيود النموذج الجديد: منع الحذف لا تتابعه، والفريد داخل الشركة ──
            migrationBuilder.CreateIndex(name: "IX_LocationLogs_ToLineId", table: "LocationLogs", column: "ToLineId");
            migrationBuilder.CreateIndex(name: "IX_LocationLogs_ToSiteId", table: "LocationLogs", column: "ToSiteId");
            migrationBuilder.CreateIndex(name: "IX_LocationLogs_ToWarehouseId", table: "LocationLogs", column: "ToWarehouseId");

            migrationBuilder.CreateIndex(name: "IX_InventoryAudits_CompanySiteId", table: "InventoryAudits", column: "CompanySiteId");
            migrationBuilder.CreateIndex(name: "IX_InventoryAudits_ProductionLineId", table: "InventoryAudits", column: "ProductionLineId");
            migrationBuilder.CreateIndex(name: "IX_InventoryAudits_WarehouseId", table: "InventoryAudits", column: "WarehouseId");

            migrationBuilder.CreateIndex(name: "IX_Assets_CompanySiteId", table: "Assets", column: "CompanySiteId");
            migrationBuilder.CreateIndex(name: "IX_Assets_ProductionLineId", table: "Assets", column: "ProductionLineId");
            migrationBuilder.CreateIndex(name: "IX_Assets_WarehouseId", table: "Assets", column: "WarehouseId");

            migrationBuilder.CreateIndex(name: "IX_CompanySites_CompanyId", table: "CompanySites", column: "CompanyId");
            migrationBuilder.CreateIndex(name: "IX_CompanySites_CompanyId_Code", table: "CompanySites",
                columns: new[] { "CompanyId", "Code" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_CompanySites_IsDeleted", table: "CompanySites", column: "IsDeleted");

            migrationBuilder.CreateIndex(name: "IX_ProductionLines_CompanyId", table: "ProductionLines", column: "CompanyId");
            migrationBuilder.CreateIndex(name: "IX_ProductionLines_CompanyId_Code", table: "ProductionLines",
                columns: new[] { "CompanyId", "Code" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ProductionLines_IsDeleted", table: "ProductionLines", column: "IsDeleted");

            migrationBuilder.CreateIndex(name: "IX_Warehouses_CompanyId", table: "Warehouses", column: "CompanyId");
            migrationBuilder.CreateIndex(name: "IX_Warehouses_CompanyId_Code", table: "Warehouses",
                columns: new[] { "CompanyId", "Code" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_Warehouses_CompanySiteId", table: "Warehouses", column: "CompanySiteId");
            migrationBuilder.CreateIndex(name: "IX_Warehouses_IsDeleted", table: "Warehouses", column: "IsDeleted");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_CompanySites_CompanySiteId", table: "Assets", column: "CompanySiteId",
                principalTable: "CompanySites", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_ProductionLines_ProductionLineId", table: "Assets", column: "ProductionLineId",
                principalTable: "ProductionLines", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Warehouses_WarehouseId", table: "Assets", column: "WarehouseId",
                principalTable: "Warehouses", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryAudits_CompanySites_CompanySiteId", table: "InventoryAudits", column: "CompanySiteId",
                principalTable: "CompanySites", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryAudits_ProductionLines_ProductionLineId", table: "InventoryAudits", column: "ProductionLineId",
                principalTable: "ProductionLines", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryAudits_Warehouses_WarehouseId", table: "InventoryAudits", column: "WarehouseId",
                principalTable: "Warehouses", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceTickets_ProductionLines_ProductionLineId", table: "MaintenanceTickets", column: "ProductionLineId",
                principalTable: "ProductionLines", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// رجوع بلا تدمير مُوهَم: جسر المعرّفات هُدم في «Up»، فأي رجوع آلي يخمّن
        /// مطابقة الأسماء والأكواد — ومطابقة خاطئة تسرق أصلًا من مخزنه. المسار
        /// الصادق: استعادة نسخة احتياطية ثم إعادة الترحيل.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new InvalidOperationException(
                "ترحيل «SplitPlacesWarehouseSiteLine» عكسه غير آمن: يفصل البيانات " +
                "من جدول مواقع واحد بثلاثة معانٍ، ويُسقط جسر المعرّفات بعد التوجيه. " +
                "الرجوع الصحيح استعادة نسخة احتياطية من قبل الترحيل ثم إعادة تشغيله.");
    }
}
