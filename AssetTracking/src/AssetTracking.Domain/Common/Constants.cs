namespace AssetTracking.Domain.Common;

/// <summary>أسماء الأدوار — لا Magic Strings في المشروع</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string CompanyManager = "CompanyManager";
    public const string Technician = "Technician";
    public const string Employee = "Employee";

    public static readonly string[] All = { Admin, CompanyManager, Technician, Employee };

    public static string ArabicName(string role) => role switch
    {
        Admin => "مدير النظام",
        CompanyManager => "مدير شركة",
        Technician => "فني دعم",
        Employee => "موظف",
        _ => role
    };
}

/// <summary>أسماء الـClaims المخصصة</summary>
public static class AppClaims
{
    /// <summary>مُعرِّف شركة المستخدم — أساس عزل البيانات</summary>
    public const string CompanyId = "company_id";

    /// <summary>الـAdmin يرى كل الشركات</summary>
    public const string AllCompanies = "all_companies";

    public const string FullName = "full_name";
    public const string DepartmentId = "department_id";
}

/// <summary>أسماء سياسات التصريح (Authorization Policies)</summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string TechnicianOrAbove = "TechnicianOrAbove";
    public const string AuthenticatedUser = "AuthenticatedUser";
}

/// <summary>مفاتيح إعدادات النظام</summary>
public static class SettingKeys
{
    public const string SmtpHost = "Smtp.Host";
    public const string SmtpPort = "Smtp.Port";
    public const string SmtpUser = "Smtp.User";
    public const string SmtpPassword = "Smtp.Password";
    public const string SmtpEnableSsl = "Smtp.EnableSsl";
    public const string SmtpFromAddress = "Smtp.FromAddress";
    public const string SmtpFromName = "Smtp.FromName";

    public const string AppBaseUrl = "App.BaseUrl";
    public const string AppName = "App.Name";
    public const string Currency = "App.Currency";
    public const string NotificationRetentionDays = "Notification.RetentionDays";
}

/// <summary>ثوابت عامة</summary>
public static class AppConstants
{
    /// <summary>رمز العملة — الجنيه المصري</summary>
    public const string CurrencySymbol = "ج.م";

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>الامتدادات المسموحة للمرفقات</summary>
    public static readonly string[] AllowedFileExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

    /// <summary>الحد الأقصى لحجم المرفق (10 ميجابايت)</summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public const string AssetTagPrefix = "AST";
    public const string TicketNumberPrefix = "TKT";
    public const string AuditCodePrefix = "AUD";
}
