using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AssetTracking.Infrastructure.Email;

/// <summary>
/// إرسال البريد عبر MailKit بإعدادات SMTP مقروءة من جدول SYSTEM_SETTINGS
/// (قابلة للتعديل من شاشة الإعدادات دون إعادة نشر).
/// تُستدعى من طابور Hangfire حتى لا يُبطئ فشل SMTP تجربة المستخدم.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(ISettingsService settings, ILogger<SmtpEmailSender> log)
    {
        _settings = settings; _log = log;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = await _settings.GetAsync(SettingKeys.SmtpHost, ct: ct);
        if (string.IsNullOrWhiteSpace(host))
        {
            _log.LogWarning("إعدادات SMTP غير مضبوطة — تم تخطي إرسال البريد إلى {To}", to);
            return;
        }

        var portRaw = await _settings.GetAsync(SettingKeys.SmtpPort, ct: ct);
        var port = int.TryParse(portRaw, out var p) ? p : 587;

        var user = await _settings.GetAsync(SettingKeys.SmtpUser, ct: ct);
        var pass = await _settings.GetAsync(SettingKeys.SmtpPassword, ct: ct);
        var fromAddr = await _settings.GetAsync(SettingKeys.SmtpFromAddress, ct: ct) ?? user ?? "no-reply@localhost";
        var fromName = await _settings.GetAsync(SettingKeys.SmtpFromName, ct: ct) ?? "نظام إدارة الأصول";
        var sslRaw = await _settings.GetAsync(SettingKeys.SmtpEnableSsl, ct: ct);
        var useSsl = !string.Equals(sslRaw, "false", StringComparison.OrdinalIgnoreCase);

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, fromAddr));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = WrapRtlTemplate(subject, htmlBody) }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port,
            useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

        if (!string.IsNullOrWhiteSpace(user))
            await client.AuthenticateAsync(user, pass ?? string.Empty, ct);

        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);

        _log.LogInformation("تم إرسال بريد إلى {To} بعنوان {Subject}", to, subject);
    }

    /// <summary>قالب HTML عربي RTL موحّد لكل الرسائل</summary>
    private static string WrapRtlTemplate(string title, string body) => $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head><meta charset=""utf-8""></head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:'Segoe UI',Tahoma,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f1f5f9;padding:24px 0;"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0""
             style=""background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06);"">
        <tr><td style=""background:#0f766e;padding:20px 24px;color:#fff;font-size:18px;font-weight:bold;"">
          نظام إدارة وتتبع الأصول والدعم الفني
        </td></tr>
        <tr><td style=""padding:24px;color:#1e293b;font-size:15px;line-height:1.9;"">
          <h2 style=""margin:0 0 12px;font-size:18px;color:#0f172a;"">{title}</h2>
          {body}
        </td></tr>
        <tr><td style=""padding:16px 24px;background:#f8fafc;color:#64748b;font-size:12px;"">
          هذه رسالة آلية — برجاء عدم الرد عليها.
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
}
