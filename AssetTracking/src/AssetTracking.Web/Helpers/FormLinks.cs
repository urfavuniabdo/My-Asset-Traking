namespace AssetTracking.Web.Helpers;

/// <summary>
/// تحويل قيم قوائم الاختيار عند حافة النموذج: قائمة تترك خيار «بدون» ترسل 0،
/// والمقصود «غير مرتبط» أي null لا المعرّف صفر.
///
/// التحويل في طبقة العرض لا في قواعد الدومين: Domain لا يجب أن يعرف شكل ‎&lt;form&gt;‎.
/// </summary>
public static class FormLinks
{
    public static int? BlankToNull(int? value) => value is null or 0 ? null : value;
}
