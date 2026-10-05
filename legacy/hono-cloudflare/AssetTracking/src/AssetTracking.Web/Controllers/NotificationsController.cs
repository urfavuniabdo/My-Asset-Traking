using AssetTracking.Domain.Common;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// إشعارات المستخدم. كل استعلام مقيّد بـ UserId الحالي —
/// لا يمكن لمستخدم رؤية إشعارات غيره حتى لو عرف المعرّف.
/// </summary>
public class NotificationsController : BaseController
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db) => _db = db;

    /// <summary>صفحة كل الإشعارات</summary>
    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1)
    {
        var me = Me.UserId;
        if (string.IsNullOrEmpty(me)) return NotFoundOrForbidden();

        page = page < 1 ? 1 : page;
        var size = AppConstants.DefaultPageSize;

        var q = _db.Notifications.Where(n => n.UserId == me);
        if (unreadOnly) q = q.Where(n => !n.IsRead);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(n => new NotificationRow
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Body,
                Url = n.ActionUrl,
                Icon = n.Icon,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                Type = n.Type
            })
            .ToListAsync();

        var vm = new NotificationListViewModel
        {
            Items = items,
            Page = page,
            PageSize = size,
            TotalCount = total,
            UnreadOnly = unreadOnly,
            UnreadCount = await _db.Notifications.CountAsync(n => n.UserId == me && !n.IsRead)
        };

        return View(vm);
    }

    /// <summary>JSON لجرس الإشعارات — أحدث ١٠ إشعارات + عدد غير المقروء</summary>
    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var me = Me.UserId;
        if (string.IsNullOrEmpty(me))
            return Json(new { items = Array.Empty<object>(), unreadCount = 0 });

        var items = await _db.Notifications
            .Where(n => n.UserId == me)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Body,
                url = n.ActionUrl,
                icon = n.Icon,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            })
            .ToListAsync();

        var unread = await _db.Notifications.CountAsync(n => n.UserId == me && !n.IsRead);

        return Json(new { items, unreadCount = unread });
    }

    /// <summary>تحديد إشعار واحد كمقروء ثم التوجيه لرابط الإجراء</summary>
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var me = Me.UserId;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me);

        // نفس صيغة الرسالة سواء كان غير موجود أو ليس ملكه
        if (n == null) return NotFoundOrForbidden();

        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Redirect(string.IsNullOrEmpty(n.ActionUrl) ? "/Notifications" : n.ActionUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var me = Me.UserId;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me);
        if (n == null) return NotFoundOrForbidden();

        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { ok = true });

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var me = Me.UserId;
        var now = DateTime.UtcNow;

        var unread = await _db.Notifications
            .Where(n => n.UserId == me && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _db.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { ok = true, count = unread.Count });

        ToastSuccess($"تم تحديد {unread.Count} إشعاراً كمقروء.");
        return RedirectToAction(nameof(Index));
    }
}
