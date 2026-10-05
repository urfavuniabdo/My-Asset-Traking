namespace AssetTracking.Domain.Enums;

/// <summary>حالة الأصل</summary>
public enum AssetStatus
{
    Active = 1,             // نشط / بالخدمة
    InStore = 2,            // في المخزن
    UnderMaintenance = 3,   // تحت الصيانة
    Damaged = 4,            // تالف
    Disposed = 5,           // مستبعد / مشطوب
    Lost = 6                // مفقود
}

/// <summary>حالة التذكرة</summary>
public enum TicketStatus
{
    Open = 1,           // مفتوحة
    Assigned = 2,       // مُكلَّف بها فني
    InProgress = 3,     // جاري العمل
    WaitingParts = 4,   // بانتظار قطع غيار
    Resolved = 5,       // تم الحل
    Closed = 6,         // مغلقة
    Cancelled = 7       // ملغاة
}

/// <summary>أولوية التذكرة (تحدد مواعيد SLA)</summary>
public enum TicketPriority
{
    Low = 1,        // منخفضة
    Medium = 2,     // متوسطة
    High = 3,       // عالية
    Critical = 4    // حرجة
}

/// <summary>نوع التذكرة</summary>
public enum TicketType
{
    Corrective = 1,     // صيانة إصلاحية
    Preventive = 2,      // صيانة وقائية
    Installation = 3,    // تركيب
    Inspection = 4       // فحص
}

/// <summary>حالة سجل العهدة</summary>
public enum CustodyStatus
{
    Pending = 1,    // بانتظار موافقة الموظف
    Accepted = 2,   // مقبولة
    Rejected = 3,   // مرفوضة
    Returned = 4    // تم الإرجاع
}

/// <summary>نوع حركة العهدة</summary>
public enum CustodyAction
{
    Assign = 1,     // تسليم
    Transfer = 2,   // نقل بين موظفين
    Return = 3      // إرجاع للمخزن
}

/// <summary>نوع الموقع</summary>
public enum LocationType
{
    Office = 1,     // مكتب / مقر إداري
    Factory = 2,    // مصنع
    Warehouse = 3,  // مخزن / مستودع
    Building = 4,   // مبنى / موقع مشروع
    Apartment = 5,  // شقة / سكن
    Branch = 6,     // فرع
    Other = 7       // أخرى
}

/// <summary>طريقة احتساب الإهلاك</summary>
public enum DepreciationMethod
{
    StraightLine = 1,       // القسط الثابت
    DecliningBalance = 2    // القسط المتناقص
}

/// <summary>حالة جرد المخزون</summary>
public enum AuditStatus
{
    Draft = 1,       // مسودة
    InProgress = 2,  // جاري التنفيذ
    Completed = 3,   // مكتمل
    Cancelled = 4    // ملغي
}

/// <summary>نتيجة بند الجرد</summary>
public enum AuditItemResult
{
    Pending = 1,        // لم يُجرد بعد
    Found = 2,          // موجود بمكانه
    Misplaced = 3,      // موجود بمكان مختلف
    Missing = 4,        // مفقود
    Damaged = 5         // تالف
}

/// <summary>حالة جدول الصيانة الوقائية</summary>
public enum ScheduleStatus
{
    Active = 1,     // مُفعَّل
    Paused = 2,     // موقوف مؤقتاً
    Ended = 3       // منتهي
}

/// <summary>تكرار الصيانة الوقائية</summary>
public enum ScheduleFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    SemiAnnual = 5,
    Annual = 6
}

/// <summary>نوع الإشعار</summary>
public enum NotificationType
{
    CustodyAssigned = 1,
    CustodyAccepted = 2,
    CustodyRejected = 3,
    CustodyReturned = 4,
    TicketCreated = 5,
    TicketAssigned = 6,
    TicketStatusChanged = 7,
    TicketResolved = 8,
    SlaBreached = 9,
    WarrantyExpiring = 10,
    MaintenanceDue = 11,
    General = 12
}

/// <summary>قناة الإشعار</summary>
public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Both = 3
}
