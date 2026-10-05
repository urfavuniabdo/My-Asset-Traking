using AssetTracking.Application.Common;
using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Infrastructure.Services;

/// <summary>
/// دورة حياة العهدة الكاملة: تسليم ← موافقة/رفض الموظف ← إرجاع.
///
/// ⚠️ قاعدة أمنية: الرد على طلب العهدة مسموح <b>للموظف المستلم فقط</b>
/// (NewUserId == المستخدم الحالي) — لا يستطيع المدير الموافقة بدلاً منه.
/// </summary>
public class CustodyService : ICustodyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _users;

    public CustodyService(AppDbContext db, ICurrentUser me, INotificationService notify,
        IAuditService audit, UserManager<ApplicationUser> users)
    {
        _db = db; _me = me; _notify = notify; _audit = audit; _users = users;
    }

    public async Task<Result<CustodyLog>> AssignAsync(int assetId, string newUserId,
        string? reason, CancellationToken ct = default)
    {
        // الفلتر العام يضمن أن الأصل من شركة المستخدم — وإلا يعود null (نعامله كـ"غير موجود")
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset == null)
            return Result<CustodyLog>.Fail("الأصل غير موجود أو لا تملك صلاحية الوصول إليه");

        if (asset.Status is AssetStatus.Disposed or AssetStatus.Lost)
            return Result<CustodyLog>.Fail("لا يمكن تسليم أصل مستبعد أو مفقود");

        var target = await _users.FindByIdAsync(newUserId);
        if (target == null || target.IsDeleted || !target.IsActive)
            return Result<CustodyLog>.Fail("الموظف المستلم غير موجود أو غير مُفعَّل");

        // الموظف يجب أن يكون من نفس شركة الأصل
        if (target.CompanyId != null && target.CompanyId != asset.CompanyId)
            return Result<CustodyLog>.Fail("لا يمكن تسليم الأصل لموظف من شركة أخرى");

        // منع طلب معلّق مزدوج على نفس الأصل
        var hasPending = await _db.CustodyLogs
            .AnyAsync(c => c.AssetId == assetId && c.Status == CustodyStatus.Pending, ct);
        if (hasPending)
            return Result<CustodyLog>.Fail("يوجد طلب عهدة معلّق على هذا الأصل بالفعل");

        var log = new CustodyLog
        {
            CompanyId = asset.CompanyId,
            AssetId = asset.Id,
            Action = asset.CurrentCustodyUserId == null ? CustodyAction.Assign : CustodyAction.Transfer,
            Status = CustodyStatus.Pending,
            PreviousUserId = asset.CurrentCustodyUserId,
            NewUserId = newUserId,
            AssignedByUserId = _me.UserId,
            ActionDate = DateTime.UtcNow,
            Reason = reason
        };

        _db.CustodyLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyAsync(newUserId, NotificationType.CustodyAssigned,
            "طلب عهدة جديد",
            $"تم تسليمك الأصل «{asset.NameAr}» ({asset.AssetTag}) — برجاء المراجعة والموافقة.",
            $"/Custody/MyCustody", asset.CompanyId, ct: ct);

        await _audit.LogAsync("CustodyAssign", nameof(Asset), asset.Id.ToString(),
            new { asset.CurrentCustodyUserId }, new { NewUserId = newUserId }, ct);

        return Result<CustodyLog>.Success(log, "تم إرسال طلب العهدة للموظف بنجاح");
    }

    public async Task<Result> RespondAsync(int custodyLogId, bool accept, string? note,
        CancellationToken ct = default)
    {
        var log = await _db.CustodyLogs.Include(c => c.Asset)
            .FirstOrDefaultAsync(c => c.Id == custodyLogId, ct);

        if (log == null)
            return Result.Fail("السجل غير موجود أو لا تملك صلاحية الوصول إليه");

        // ⚠️ الحاجز الأمني: المستلم فقط يرد
        if (log.NewUserId != _me.UserId)
            return Result.Fail("السجل غير موجود أو لا تملك صلاحية الوصول إليه");

        if (log.Status != CustodyStatus.Pending)
            return Result.Fail("تم الرد على هذا الطلب مسبقاً");

        var asset = log.Asset!;

        log.Status = accept ? CustodyStatus.Accepted : CustodyStatus.Rejected;
        log.RespondedAt = DateTime.UtcNow;
        log.ResponseNote = note;

        if (accept)
        {
            asset.CurrentCustodyUserId = log.NewUserId;
            asset.CustodySince = DateTime.UtcNow;
            if (asset.Status == AssetStatus.InStore) asset.Status = AssetStatus.Active;
        }

        await _db.SaveChangesAsync(ct);

        if (log.AssignedByUserId != null)
        {
            await _notify.NotifyAsync(log.AssignedByUserId,
                accept ? NotificationType.CustodyAccepted : NotificationType.CustodyRejected,
                accept ? "تم قبول العهدة" : "تم رفض العهدة",
                $"الأصل «{asset.NameAr}» ({asset.AssetTag}) — " +
                (accept ? "قَبِل الموظف العهدة." : $"رفض الموظف العهدة. السبب: {note ?? "غير مذكور"}"),
                $"/Assets/Details/{asset.Id}", asset.CompanyId, ct: ct);
        }

        await _audit.LogAsync(accept ? "CustodyAccept" : "CustodyReject",
            nameof(CustodyLog), log.Id.ToString(), null, new { accept, note }, ct);

        return Result.Success(accept ? "تم قبول العهدة بنجاح" : "تم رفض العهدة");
    }

    public async Task<Result> ReturnAsync(int assetId, string? reason, string? condition,
        CancellationToken ct = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset == null)
            return Result.Fail("الأصل غير موجود أو لا تملك صلاحية الوصول إليه");

        if (asset.CurrentCustodyUserId == null)
            return Result.Fail("هذا الأصل ليس في عهدة أي موظف حالياً");

        var previous = asset.CurrentCustodyUserId;

        _db.CustodyLogs.Add(new CustodyLog
        {
            CompanyId = asset.CompanyId,
            AssetId = asset.Id,
            Action = CustodyAction.Return,
            Status = CustodyStatus.Returned,
            PreviousUserId = previous,
            NewUserId = null,
            AssignedByUserId = _me.UserId,
            ActionDate = DateTime.UtcNow,
            RespondedAt = DateTime.UtcNow,
            Reason = reason,
            ConditionNote = condition
        });

        asset.CurrentCustodyUserId = null;
        asset.CustodySince = null;
        if (asset.Status == AssetStatus.Active) asset.Status = AssetStatus.InStore;

        await _db.SaveChangesAsync(ct);

        await _notify.NotifyAsync(previous, NotificationType.CustodyReturned,
            "تم إرجاع العهدة",
            $"تم إرجاع الأصل «{asset.NameAr}» ({asset.AssetTag}) للمخزن.",
            $"/Assets/Details/{asset.Id}", asset.CompanyId, ct: ct);

        await _audit.LogAsync("CustodyReturn", nameof(Asset), asset.Id.ToString(),
            new { PreviousUserId = previous }, new { reason, condition }, ct);

        return Result.Success("تم إرجاع العهدة للمخزن بنجاح");
    }
}
