using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور يجب أن تكون ٨ أحرف على الأقل")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
}
