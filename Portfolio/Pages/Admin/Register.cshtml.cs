using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;
using Portfolio.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;

namespace Portfolio.Pages.Admin
{
  [AllowAnonymous]
  public class RegisterModel : PageModel
  {
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IMailSender _mailSender;
    private readonly IConfiguration _cfg;
    private readonly ILogger<RegisterModel> _logger;
    public RegisterModel(UserManager<IdentityUser> userManager, IMailSender mailSender, IConfiguration cfg, ILogger<RegisterModel> logger)
    {
      _userManager = userManager;
      _mailSender = mailSender;
      _cfg = cfg;
      _logger = logger;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();
    public string? Error { get; set; }
    public string? Message { get; set; }

    public class RegisterInput
    {
      /// <summary>
      ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
      ///     directly from your code. This API may change or be removed in future releases.
      /// </summary>
      [Required]
      [EmailAddress]
      [Display(Name = "Email")]
      public string Email { get; set; } = string.Empty;

      /// <summary>
      ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
      ///     directly from your code. This API may change or be removed in future releases.
      /// </summary>
      [Required]
      [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
      [DataType(DataType.Password)]
      [Display(Name = "Password")]
      public string Password { get; set; } = string.Empty;

      /// <summary>
      ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
      ///     directly from your code. This API may change or be removed in future releases.
      /// </summary>
      [DataType(DataType.Password)]
      [Display(Name = "Confirm password")]
      [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
      public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
      if (!ModelState.IsValid)
      {
        Error = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        _logger.LogInformation(Error);

        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "Registration failed.";

        return Page();
      }

      if (await _userManager.FindByEmailAsync(Input.Email) is not null)
      {
        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "Email is already registered.";
        return Page();
      }

      var user = new IdentityUser
      {
        UserName = Input.Email,
        Email = Input.Email
      };

      var result = await _userManager.CreateAsync(user, Input.Password);

      if (!result.Succeeded)
      {
        Error = string.Join("; ", result.Errors.Select(e => e.Description));
        return Page();
      }

      // Send email confirmation using _emailSender
      var userId = await _userManager.GetUserIdAsync(user);
      var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
      var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
      var confirmationUrl = Url.Page(
          "/Admin/ConfirmEmail",
          pageHandler: null,
          values: new { userId = user.Id, token = encodedToken },
          protocol: Request.Scheme
      );

      try
      {
        var fromEmail = _cfg["EmailSettings:From"] ?? _cfg["EmailSettings:Username"] ?? "noreply@example.com";
        var fromName = _cfg["Site:Name"] ?? "Rados AI";

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, fromEmail));
        msg.To.Add(new MailboxAddress(Input.Email, Input.Email));
        msg.Subject = "Confirm your email";
        msg.Body = new TextPart("html")
        {
          Text = $@"
                            <!DOCTYPE html>
                            <html lang='en'>
                            <body style='margin:0; padding:0; background-color:#0b0b0f;'>
                              <table width='100%' bgcolor='#0b0b0f' cellpadding='0' cellspacing='0' style='font-family: Inter, Arial, sans-serif;'>
                                <tr>
                                  <td align='center'>
                                    <table width='480' cellpadding='0' cellspacing='0' style='background-color:#181824; border-radius:12px; box-shadow:0 2px 8px rgba(0,0,0,0.2); margin:40px 0;'>
                                      <tr>
                                        <td style='padding:32px; color:#f8f9fa;'>
                                          <h3 style='color:#0d6efd; font-family: Space Grotesk, Inter, Arial, sans-serif; margin-bottom:16px;'>Welcome to Rados Portfolio!</h3>
                                          <p style='font-size:16px; margin-bottom:24px;'>Thank you for registering. Please confirm your account by clicking the button below:</p>
                                          <table cellpadding='0' cellspacing='0' style='margin:24px 0;'>
                                            <tr>
                                              <td align='center'>
                                                <a href='{HtmlEncoder.Default.Encode(confirmationUrl)}'
                                                   style='display:inline-block; background-color:#0d6efd; color:#fff; padding:12px 24px; border-radius:6px; text-decoration:none; font-weight:600; font-family:inherit; border:1px solid #0d6efd;'>
                                                  Confirm Email
                                                </a>
                                              </td>
                                            </tr>
                                          </table>
                                          <div style='margin-top:32px; font-size:0.9em; color:#adb5bd;'>
                                            If you did not register, you can safely ignore this email.<br>
                                            &copy; Rados AI Portfolio
                                          </div>
                                        </td>
                                      </tr>
                                    </table>
                                  </td>
                                </tr>
                              </table>
                            </body>
                            </html>
                            "
        };
        await _mailSender.SendAsync(msg);
      }
      catch (Exception ex)
      {
        _logger.LogInformation(ex.Message);

        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "User created but email failed: ";

        return Page();
      }
      _logger.LogInformation("Registration successful");

      TempData["ToastType"] = "success";
      TempData["ToastMessage"] = "Registration successful. Please check your email to confirm your account.";
      return RedirectToPage("/Admin/Login");
    }

  }
}
