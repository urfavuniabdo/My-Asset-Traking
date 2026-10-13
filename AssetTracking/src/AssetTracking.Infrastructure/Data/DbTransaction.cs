namespace AssetTracking.Infrastructure.Data;

/// <summary>
/// تعهّد قاعدة بيانات لمجموعة كتابات واحدة.
///
/// لماذا نحتاجه: كل `SaveChangesAsync` ذرّة واحدة ذرّية في EF Core، لكن الـflows
/// التي تحفظ أكثر من مرة (عهدة، تذكرة، توليد صيانة دورية) لها نافذة فشل بينهما:
/// التذكرة تُحفظ ثم يفشل سجل الحالة، فيبقى الأصل بلا خط زمني، أو تُحدَّث
/// «الموعد التالي» ثم يفشل إنشاء التذكرة فتُفقد دورة صيانة كاملة.
///
/// السلوك: إن كانت هناك معاملة مفتوحة بالفعل على نفس السياق ننضم إليها بدل
/// أن نفتح واحدة ثانية (EF لا يسمح بمعاملة متداخلة على نفس الاتصال).
/// </summary>
public static class DbTransaction
{

    public static async Task RunAsync(AppDbContext db, Func<Task> work, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            await work();
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// تعهّد مشروط: تُرجع الدالة false عندما ترفض الحالة فتتراجع الكتابة كلها بلا
    /// استثناء، والـ true تُلزِمها. الاستثناء يبقى للأعطال الحقيقية (كما في RunAsync).
    ///
    /// لماذا يلزم: فشل Identity يُبلَّغ كـ IdentityResult لا كاستثناء. لو تُركت النتيجة
    /// بلا فحص التزم النصف: حساب بلا دور، أو كلمة مرور تغيّرت بلا علَم ولا أثر تدقيقي.
    /// واللفّ باستثناء صناعي يحوّل رفض مدخلات صحيح إلى صفحة خطأ، وهذا أفسد سلوك النموذج.
    ///
    /// كل مستدعي هذا المسار اليوم على القمة (أفعال كونترولرات بلا معاملة محيطة)، فالرفض
    /// يرجع لصاحب التعهّد يُلغيه. وإن وُجدت معاملة محيطة ننضم إليها — EF يرفض الثانية —
    /// ونعيد القرار لصاحبها لأنه وحده القادر على إلغاء الكل.
    /// </summary>
    public static async Task<bool> RunIfAsync(AppDbContext db, Func<Task<bool>> work,
        CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null) return await work();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await work())
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await tx.CommitAsync(ct);
        return true;
    }
}
