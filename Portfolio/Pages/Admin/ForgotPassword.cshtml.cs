using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MimeKit;
using Portfolio.Services;

namespace Portfolio.Pages.Admin
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IMailSender _mailSender;
        private readonly IConfiguration _cfg;
        public ForgotPasswordModel(UserManager<IdentityUser> userManager, IMailSender mailSender, IConfiguration cfg)
        { _userManager = userManager; _mailSender = mailSender; _cfg = cfg; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        [BindProperty]
        public InputModel Input { get; set; } = new();
        public class InputModel { [Required, EmailAddress] public string Email { get; set; } = string.Empty; }
        public void OnGet() { }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { Error = "Email required."; return Page(); }
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                Message = "If that email exists, a reset link has been sent."; return Page();
            }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Page("/Admin/ResetPassword", null, new { userId = user.Id, token }, Request.Scheme);
            try
            {
                var fromEmail = _cfg["EmailSettings:From"] ?? _cfg["EmailSettings:Username"] ?? "noreply@example.com";
                var fromName = _cfg["Site:Name"] ?? "Rados AI";

                var msg = new MimeMessage();
                msg.From.Add(new MailboxAddress(fromName, fromEmail));
                msg.To.Add(new MailboxAddress(Input.Email, Input.Email));
                msg.Subject = "Password Reset";
                msg.Body = new TextPart("html") { Text = $"<p>Reset your password by clicking <a href='{link}'>this link</a>.</p>" };
                await _mailSender.SendAsync(msg);
            }
            catch (Exception ex)
            {
                Error = "Failed to send email: " + ex.Message;
                return Page();
            }
            Message = "If that email exists, a reset link has been sent.";
            return Page();
        }
    }
}
