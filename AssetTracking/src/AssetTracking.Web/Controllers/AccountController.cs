using AssetTracking.Domain.Common;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AssetTracking.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AccountController> _log;

    public AccountController(SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users, ILogger<AccountController> log)
    {
        _signIn = signIn; _users = users; _log = log;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email);

        // رسالة واحدة موحّدة — لا نكشف إن كان البريد موجوداً أم لا
        if (user == null || user.IsDeleted || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password,
            model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "تم قفل الحساب مؤقتاً بعد عدة محاولات فاشلة. برجاء المحاولة بعد ١٥ دقيقة.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            return View(model);
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _users.UpdateAsync(user);

        _log.LogInformation("تسجيل دخول ناجح: {Email}", user.Email);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        var roles = await _users.GetRolesAsync(user);

        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            JobTitle = user.JobTitle,
            EmployeeNumber = user.EmployeeNumber,
            PhoneNumber = user.PhoneNumber,
            RoleName = roles.FirstOrDefault() is { } r ? Roles.ArabicName(r) : "—",
            LastLoginAt = user.LastLoginAt
        });
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        var res = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        user.MustChangePassword = false;
        await _users.UpdateAsync(user);
        await _signIn.RefreshSignInAsync(user);

        TempData["ToastSuccess"] = "تم تغيير كلمة المرور بنجاح.";
        return RedirectToAction(nameof(Profile));
    }
}

/// <summary>يضيف Claims المخصصة (الشركة، الاسم الكامل) لكل جلسة</summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public AppClaimsPrincipalFactory(UserManager<ApplicationUser> um,
        RoleManager<ApplicationRole> rm, IOptions<IdentityOptions> options)
        : base(um, rm, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var id = await base.GenerateClaimsAsync(user);

        id.AddClaim(new Claim(AppClaims.FullName, user.FullName));

        if (user.CompanyId.HasValue)
            id.AddClaim(new Claim(AppClaims.CompanyId, user.CompanyId.Value.ToString()));

        if (user.DepartmentId.HasValue)
            id.AddClaim(new Claim(AppClaims.DepartmentId, user.DepartmentId.Value.ToString()));

        return id;
    }
}
