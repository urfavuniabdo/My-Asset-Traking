using AssetTracking.Application.Common;
using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Infrastructure.Services;

/// <summary>
/// حارس الكود داخل نطاق الشركة — قاعدة واحدة للكيانات الثلاثة.
///
/// لماذا مساعد مشترك لا ثلاثة نصوص متشابهة: التفرد محكوم بفهرس فريد على
/// (CompanyId, Code)، فيجب أن يطابق فحص الخدمة الفهرس تمامًا — وإلا مرّ طلب
/// من الخدمة وانفجر عند القاعدة برسالة غير مفهومة. الفحص يشمل المحذوفين
/// لأن الفهرس لا يستثنيهم.
/// </summary>
internal static class CompanyScopedCodes
{
    public static async Task<bool> IsTakenAsync<TEntity>(this AppDbContext db,
        string? code, int companyId, int selfId, CancellationToken ct = default)
        where TEntity : BaseEntity, ICompanyOwned, ICompanyScopedCode
        => code != null && await db.Set<TEntity>().IgnoreQueryFilters()
            .AnyAsync(e => e.Code == code && e.CompanyId == companyId && e.Id != selfId, ct);
}

/// <summary>
/// الكاتب الوحيد لمواقع الشركة. لا يعرف شيئًا عن المخازن ولا يملك عمود «نوع»:
/// الفعل الذي كان يحوّل الطلب إلى مخزن أو موقع بحسب كلمة في النموذج هو جذر العيب.
/// </summary>
public class CompanySiteService : ICompanySiteService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;

    public CompanySiteService(AppDbContext db, ICurrentUser me, IAuditService audit)
    {
        _db = db; _me = me; _audit = audit;
    }

    public async Task<Result<CompanySite>> CreateAsync(CompanySiteInput input,
        CancellationToken ct = default)
    {
        var companyId = _me.CompanyId ?? input.CompanyId;
        if (companyId == null) return Result<CompanySite>.Fail("يجب تحديد الشركة");

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<CompanySite>(code, companyId.Value, 0, ct))
            return Result<CompanySite>.Fail($"كود الموقع «{code}» مستخدم في هذه الشركة");

        var e = new CompanySite { CompanyId = companyId.Value };
        Map(input, e);
        _db.CompanySites.Add(e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("CreateCompanySite", nameof(CompanySite), e.Id.ToString(),
                null, e.NameAr, ct);
        }, ct);

        return Result<CompanySite>.Success(e, $"تمت إضافة موقع الشركة «{e.NameAr}»");
    }

    public async Task<Result<CompanySite>> UpdateAsync(CompanySiteInput input,
        CancellationToken ct = default)
    {
        var e = await _db.CompanySites.FirstOrDefaultAsync(s => s.Id == input.Id, ct);
        if (e == null) return Result<CompanySite>.Fail(Messages.HiddenRecord);

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<CompanySite>(code, e.CompanyId, e.Id, ct))
            return Result<CompanySite>.Fail($"كود الموقع «{code}» مستخدم في هذه الشركة");

        var before = e.NameAr;
        Map(input, e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("EditCompanySite", nameof(CompanySite), e.Id.ToString(),
                before, e.NameAr, ct);
        }, ct);

        return Result<CompanySite>.Success(e, $"تم تحديث موقع الشركة «{e.NameAr}»");
    }

    private static void Map(CompanySiteInput input, CompanySite e)
    {
        e.NameAr = input.NameAr.Trim();
        e.NameEn = NullIfBlank(input.NameEn);
        e.Code = NullIfBlank(input.Code);
        e.Kind = input.Kind;
        e.Address = NullIfBlank(input.Address);
        e.City = NullIfBlank(input.City);
        e.Governorate = NullIfBlank(input.Governorate);
        e.ContactPerson = NullIfBlank(input.ContactPerson);
        e.ContactPhone = NullIfBlank(input.ContactPhone);
        e.Notes = NullIfBlank(input.Notes);
        e.IsActive = input.IsActive;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// الكاتب الوحيد للمخازن. الموقع المرتبط اختياري تمامًا: null وضع تشغيلي كامل،
/// وإن وُجد فيجب أن يكون موقعًا لشركة المخزن نفسها — الحارس هنا لا الفلتر العام،
/// لأن الفلتر معطَّل لمدير النظام فالمقارنة صريحة مع IgnoreQueryFilters.
/// </summary>
public class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;

    public WarehouseService(AppDbContext db, ICurrentUser me, IAuditService audit)
    {
        _db = db; _me = me; _audit = audit;
    }

    public async Task<Result<Warehouse>> CreateAsync(WarehouseInput input,
        CancellationToken ct = default)
    {
        var companyId = _me.CompanyId ?? input.CompanyId;
        if (companyId == null) return Result<Warehouse>.Fail("يجب تحديد الشركة");

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<Warehouse>(code, companyId.Value, 0, ct))
            return Result<Warehouse>.Fail($"كود المخزن «{code}» مستخدم في هذه الشركة");

        if (!await _db.OwnedByCompanyAsync<CompanySite>(input.CompanySiteId, companyId.Value, ct))
            return Result<Warehouse>.Fail(Messages.HiddenRecord);

        var e = new Warehouse { CompanyId = companyId.Value };
        Map(input, e);
        _db.Warehouses.Add(e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("CreateWarehouse", nameof(Warehouse), e.Id.ToString(),
                null, e.NameAr, ct);
        }, ct);

        return Result<Warehouse>.Success(e, $"تمت إضافة المخزن «{e.NameAr}»");
    }

    public async Task<Result<Warehouse>> UpdateAsync(WarehouseInput input,
        CancellationToken ct = default)
    {
        var e = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == input.Id, ct);
        if (e == null) return Result<Warehouse>.Fail(Messages.HiddenRecord);

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<Warehouse>(code, e.CompanyId, e.Id, ct))
            return Result<Warehouse>.Fail($"كود المخزن «{code}» مستخدم في هذه الشركة");

        if (!await _db.OwnedByCompanyAsync<CompanySite>(input.CompanySiteId, e.CompanyId, ct))
            return Result<Warehouse>.Fail(Messages.HiddenRecord);

        var before = e.NameAr;
        Map(input, e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("EditWarehouse", nameof(Warehouse), e.Id.ToString(),
                before, e.NameAr, ct);
        }, ct);

        return Result<Warehouse>.Success(e, $"تم تحديث المخزن «{e.NameAr}»");
    }

    private static void Map(WarehouseInput input, Warehouse e)
    {
        e.NameAr = input.NameAr.Trim();
        e.NameEn = NullIfBlank(input.NameEn);
        e.Code = NullIfBlank(input.Code);
        e.Notes = NullIfBlank(input.Notes);
        e.Address = NullIfBlank(input.Address);
        e.CompanySiteId = input.CompanySiteId is null or 0 ? null : input.CompanySiteId;
        e.IsActive = input.IsActive;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// الكاتب الوحيد لخطوط الإنتاج. لا يملك عمود «نوع» ولا مرجع موضع: الإجابة على
/// «على أي خط يعمل الأصل؟» لا تُستمد من المخزن ولا من فرع الشركة، والعكس ممنوع.
/// </summary>
public class ProductionLineService : IProductionLineService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;

    public ProductionLineService(AppDbContext db, ICurrentUser me, IAuditService audit)
    {
        _db = db; _me = me; _audit = audit;
    }

    public async Task<Result<ProductionLine>> CreateAsync(ProductionLineInput input,
        CancellationToken ct = default)
    {
        var companyId = _me.CompanyId ?? input.CompanyId;
        if (companyId == null) return Result<ProductionLine>.Fail("يجب تحديد الشركة");

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<ProductionLine>(code, companyId.Value, 0, ct))
            return Result<ProductionLine>.Fail($"كود الخط «{code}» مستخدم في هذه الشركة");

        var e = new ProductionLine { CompanyId = companyId.Value };
        Map(input, e);
        _db.ProductionLines.Add(e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("CreateProductionLine", nameof(ProductionLine), e.Id.ToString(),
                null, e.NameAr, ct);
        }, ct);

        return Result<ProductionLine>.Success(e, $"تمت إضافة خط الإنتاج «{e.NameAr}»");
    }

    public async Task<Result<ProductionLine>> UpdateAsync(ProductionLineInput input,
        CancellationToken ct = default)
    {
        var e = await _db.ProductionLines.FirstOrDefaultAsync(l => l.Id == input.Id, ct);
        if (e == null) return Result<ProductionLine>.Fail(Messages.HiddenRecord);

        var code = NullIfBlank(input.Code);
        if (await _db.IsTakenAsync<ProductionLine>(code, e.CompanyId, e.Id, ct))
            return Result<ProductionLine>.Fail($"كود الخط «{code}» مستخدم في هذه الشركة");

        var before = e.NameAr;
        Map(input, e);

        await DbTransaction.RunAsync(_db, async () =>
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogTextAsync("EditProductionLine", nameof(ProductionLine), e.Id.ToString(),
                before, e.NameAr, ct);
        }, ct);

        return Result<ProductionLine>.Success(e, $"تم تحديث خط الإنتاج «{e.NameAr}»");
    }

    private static void Map(ProductionLineInput input, ProductionLine e)
    {
        e.NameAr = input.NameAr.Trim();
        e.NameEn = NullIfBlank(input.NameEn);
        e.Code = NullIfBlank(input.Code);
        e.Notes = NullIfBlank(input.Notes);
        e.IsActive = input.IsActive;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
