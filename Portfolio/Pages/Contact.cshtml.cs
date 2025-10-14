using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MimeKit;
using PreMailer.Net;
using Portfolio.Services;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Portfolio.Pages
{
  public class ContactModel : PageModel
  {
    [BindProperty]
    public ContactInput Input { get; set; } = new();
    public bool Sent { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    private readonly MailQueueService _mailQueue;
    private readonly IConfiguration _cfg;


    public ContactModel(MailQueueService mailQueue, IConfiguration cfg)
    {
      _mailQueue = mailQueue;
      _cfg = cfg;

    }


    public record class ContactInput
    {
      public string? Name { get; set; }

      [Required(ErrorMessage = "Email is required.")]
      [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
      public string? Email { get; set; }

      public string? Subject { get; set; }
      public string? Message { get; set; }
    }

    public void OnGet()
    {
      if (TempData["ToastType"]?.ToString() == "success")
        SuccessMessage = TempData["ToastMessage"]?.ToString();
      if (TempData["ToastType"]?.ToString() == "error")
        ErrorMessage = TempData["ToastMessage"]?.ToString();
    }

    public IActionResult OnPost()
    {
      if (string.IsNullOrWhiteSpace(Input.Name) ||
          string.IsNullOrWhiteSpace(Input.Email) ||
          string.IsNullOrWhiteSpace(Input.Subject) ||
          string.IsNullOrWhiteSpace(Input.Message))
      {
        ModelState.AddModelError(string.Empty, "All fields are required.");
        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "All fields are required.";
        ErrorMessage = "All fields are required.";
        return Page();
      }

      // Strict email validation
      if (!IsValidEmail(Input.Email))
      {
        ModelState.AddModelError(string.Empty, "Please enter a valid email address.");
        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "Please enter a valid email address.";
        ErrorMessage = "Please enter a valid email address.";
        return Page();
      }

      if (!MailboxAddress.TryParse($"{Input.Name} <{Input.Email}>", out var userAddress))
      {
        ModelState.AddModelError(string.Empty, "Please enter a valid name and email address.");
        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "Please enter a valid name and email address.";
        ErrorMessage = "Please enter a valid name and email address.";
        return Page();
      }

      var message = new MimeMessage();
      var fromEmail = _cfg["EmailSettings:From"] ?? _cfg["EmailSettings:Username"] ?? "noreply@example.com";
      var siteFrom = new MailboxAddress("Rados", fromEmail);
      message.From.Add(siteFrom);
      message.Sender = siteFrom;
      message.ReplyTo.Add(userAddress);
      var toEmail = _cfg["EmailSettings:Username"] ?? fromEmail;
      message.To.Add(new MailboxAddress("Rados", toEmail));
      message.Subject = Input.Subject;
      var htmlBody = CreateEmailWithSignature(Input.Name!, Input.Email!, Input.Message!, Input.Subject!);
      message.Body = new TextPart("html") { Text = htmlBody };

      try
      {
        _mailQueue.Enqueue(message);
        Sent = true;
        TempData["ToastType"] = "success";
        TempData["ToastMessage"] = "Message was sent!";
        SuccessMessage = "Message was sent!";
        // Redirect to Contact so TempData survives and toast is shown
        return RedirectToPage("/Contact");
      }
      catch
      {
        Sent = false;
        TempData["ToastType"] = "error";
        TempData["ToastMessage"] = "Message was not sent! Please try later!";
        ErrorMessage = "Message was not sent! Please try later!";
        ModelState.AddModelError(string.Empty, "Failed to queue email. Configure SMTP in EmailSettings.");
        return Page();
      }
    }

    private string CreateEmailWithSignature(string senderName, string senderEmail, string messageContent, string subject)
    {
      // Sanitize user-provided values and preserve line breaks
      var safeName = WebUtility.HtmlEncode(senderName);
      var safeEmail = WebUtility.HtmlEncode(senderEmail);
      var safeSubject = WebUtility.HtmlEncode(subject);
      var safeMessage = WebUtility.HtmlEncode(messageContent)
          .Replace("\r\n", "<br>")
          .Replace("\n", "<br>")
          .Replace("\r", "<br>");

      // Complete HTML template with dynamic content
      var htmlTemplate = $@"
                                <!doctype html>
                                <html>
                                <head>
                                  <meta name=""viewport"" content=""width=device-width"">
                                  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"">
                                  <title>Contact Form - {safeSubject}</title>
                                  <link href=""https://fonts.googleapis.com/css2?family=Noto+Sans:wght@300;400;500;600&display=swap"" rel=""stylesheet"">
                                  <style>
                                    {GetEmailStyles()}
                                  </style>
                                </head>
                                <body>
                                  <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""body"" width=""100%"" bgcolor=""#161f33"">
                                    <tr>
                                      <td align=""left"">
                                        <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""container"" align=""left"">
                                          <tr>
                                            <td>
                                              <!-- Contact Form Content -->
                                              <table role=""presentation"" class=""main"" width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                                <tr>
                                                  <td class=""wrapper"" valign=""top"">
                                                    <h2 style=""color: #06090f; font-size: 22px; margin-bottom: 20px;"">New Contact Form Message</h2>
                    
                                                    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 24px; border-left: 4px solid #ec0867;"">
                                                      <p style=""margin: 0 0 12px; font-weight: 600; color: #06090f;"">From:</p>
                                                      <p style=""margin: 0 0 16px; color: #1b2a49;"">{safeName} &lt;{safeEmail}&gt;</p>
                      
                                                      <p style=""margin: 0 0 12px; font-weight: 600; color: #06090f;"">Subject:</p>
                                                      <p style=""margin: 0 0 16px; color: #1b2a49;"">{safeSubject}</p>
                      
                                                      <p style=""margin: 0 0 12px; font-weight: 600; color: #06090f;"">Message:</p>
                                                      <p style=""margin: 0; color: #1b2a49; line-height: 1.6;"">{safeMessage}</p>
                                                    </div>

                                                    <!-- Professional Signature -->
                                                    <hr style=""border: none; border-top: 1px solid #e1e5e9; margin: 30px 0 20px;"">
                    
                                                    <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" class=""signature-layout"">
                                                      <tr>
                                                        <td class=""headshot-cell"" width=""120"" valign=""top"">
                                                          <a href=""https://rajcicrados.rs/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=profile_image""
                                                            aria-label=""Visit Rajcic Rados website"">
                                                            <img class=""profile-photo"" src=""https://rralebg.github.io/assets/images/profile.png""
                                                              height=""110"" width=""110"" alt=""Portrait of Rajcic Rados"">
                                                          </a>
                                                        </td>
                                                        <td valign=""top"">
                                                          <p class=""intro"">With respect,</p>
                                                          <h1 class=""signature-name"">Rajcic Rados</h1>
                                                          <p class=""role"">Software developer</p>
                                                          <p class=""location"">Belgrade, Serbia</p>
                                                          <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""btn btn-primary"">
                                                            <tr>
                                                              <td class=""btn-padding"" align=""left"" valign=""top"">
                                                                <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0""> 
                                                                  <tr>
                                                                    <td class=""btn-primary-cell"" valign=""top"" align=""left"" bgcolor=""#812f51"">
                                                                      <a href=""https://rajcicrados.rs/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=cta_button""
                                                                        target=""_blank"" rel=""noopener"">VISIT MY WEBSITE</a>
                                                                    </td>
                                                                  </tr>
                                                                </table>
                                                              </td>
                                                            </tr>
                                                          </table>
                                                          <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""contact-list"" width=""100%"">
                                                            <tr>
                                                              <td>
                                                                <a class=""contact-icon-link"" href=""https://rajcicrados.rs/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=contact_website"" aria-label=""Visit Rajcic Rados website"">
                                                                  <img src=""https://img.icons8.com/material-outlined/24/415476/domain.png"" alt=""Website"">
                                                                </a>
                                                              </td>
                                                              <td>
                                                                <a class=""contact-icon-link"" href=""mailto:rados@rajcicrados.rs"" aria-label=""Email Rajcic Rados"">
                                                                  <img src=""https://img.icons8.com/material-outlined/24/415476/new-post.png"" alt=""Email"">
                                                                </a>
                                                              </td>
                                                              <td>
                                                                <a class=""contact-icon-link"" href=""https://www.linkedin.com/in/radoš-rajčić/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=contact_linkedin"" aria-label=""Connect with Rajcic Rados on LinkedIn"">
                                                                  <img src=""https://img.icons8.com/material-outlined/24/415476/linkedin.png"" alt=""LinkedIn"">
                                                                </a>
                                                              </td>
                                                              <td>
                                                                <a class=""contact-icon-link"" href=""https://github.com/RRaleBG?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=contact_github"" aria-label=""Follow Rajcic Rados on GitHub"">
                                                                  <img src=""https://img.icons8.com/material-outlined/24/415476/github.png"" alt=""GitHub"">
                                                                </a>
                                                              </td>
                                                            </tr>
                                                          </table>
                                                        </td>
                                                      </tr>
                                                    </table>
                                                  </td>
                                                </tr>
                                              </table>
                                            </td>
                                          </tr>
          
                                          <!-- Footer -->
                                          <tr>
                                            <td>
                                              <div class=""footer"">
                                                <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""footer-inner"" align=""center"">
                                                  <tr>
                                                    <td class=""content-block"">
                                                      <span class=""apple-link""></span><br>
                                                      Don't like these emails?
                                                      <a href=""https://rajcicrados.rs/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=unsubscribe"" class=""unsubscribe"">Unsubscribe</a>.
                                                    </td>
                                                  </tr>
                                                  <tr>
                                                    <td class=""content-block powered-by"">
                                                      Powered by
                                                      <a href=""https://rajcicrados.rs/?utm_source=email_signature&utm_medium=email&utm_campaign=signature&utm_content=footer_signature"">Rajcic Rados</a>.
                                                    </td>
                                                  </tr>
                                                  <tr>
                                                    <td class=""text-center"" style=""color: #888;"">
                                                      This message and any attachments are intended solely for the named recipient and may contain confidential or privileged information. If you received it in error, please delete it and notify the sender immediately. Any unauthorized review, use, or distribution is prohibited.
                                                    </td>
                                                  </tr>
                                                </table>
                                              </div>
                                            </td>
                                          </tr>
                                        </table>
                                      </td>
                                    </tr>
                                  </table>
                                </body>
                                </html>";

      // Inline CSS for better email client compatibility
      var inlineResult = PreMailer.Net.PreMailer.MoveCssInline(htmlTemplate, removeStyleElements: true);
      return inlineResult.Html;
    }

    private string GetEmailStyles()
    {
      return @"
    /* Mobile Responsive */
    @media only screen and (max-width: 620px) {
      table[class='body'] h1 {
        font-size: 28px !important;
        margin-bottom: 10px !important;
      }
      table[class='body'] p,
      table[class='body'] ul,
      table[class='body'] ol,
      table[class='body'] td,
      table[class='body'] span,
      table[class='body'] a {
        font-size: 16px !important;
      }
      table[class='body'] .wrapper,
      table[class='body'] .article {
        padding: 12px !important;
      }
      table[class='body'] .container {
        padding: 0 !important;
        width: 100% !important;
      }
      table[class='body'] .main {
        border-radius: 0 !important;
        width: 100% !important;
      }
      table[class='body'] .btn table {
        width: 100% !important;
      }
      table[class='body'] .btn a {
        width: 100% !important;
      }
      table[class='body'] .img-responsive {
        height: auto !important;
        max-width: 100% !important;
        width: auto !important;
      }
      .footer-inner {
        padding-left: 0 !important;
      }
      .headshot-cell {
        display: block;
        padding-right: 0;
        padding-bottom: 12px;
      }
      .signature-layout {
        width: 100% !important;
      }
      .footer-inner {
        width: 100% !important;
      }
    }

    /* Email Client Resets */
    @media all {
      .ExternalClass {
        width: 100%;
      }
      .ExternalClass,
      .ExternalClass p,
      .ExternalClass span,
      .ExternalClass font,
      .ExternalClass td,
      .ExternalClass div {
        line-height: 100%;
      }
      .apple-link a {
        color: inherit !important;
        font-family: inherit !important;
        font-size: inherit !important;
        font-weight: inherit !important;
        line-height: inherit !important;
        text-decoration: none !important;
      }
    }

    /* Base Styles */
    body {
      width: 100% !important;
      -webkit-text-size-adjust: none;
      -ms-text-size-adjust: none;
      background-color: #161f33;
      font-family: 'Noto Sans', 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
      -webkit-font-smoothing: antialiased;
      font-size: 14px;
      line-height: 1.4;
      margin: 0;
      padding: 0;
      color: #06090f;
    }

    body,
    table,
    td,
    a {
      -webkit-text-size-adjust: 100%;
      -ms-text-size-adjust: 100%;
      -webkit-font-smoothing: antialiased;
      color: inherit;
      text-decoration: none;
      font-family: inherit;
    }

    a {
      color: #ec0867;
    }

    table {
      border-collapse: separate;
      -mso-table-lspace: 0pt;
      -mso-table-rspace: 0pt;
    }

    table.body {
      border-collapse: separate;
      min-width: 100%;
      background-color: #161f33;
      width: 100%;
    }

    td {
      font-family: inherit;
      font-size: 14px;
      vertical-align: top;
    }

    /* Layout */
    table.container {
      width: 580px;
      max-width: 100%;
      margin: 0;
      padding: 0 0 24px 24px;
    }

    .content {
      display: block;
      margin: 0;
      padding: 0;
      width: 100%;
    }

    .preheader {
      color: transparent;
      display: none;
      height: 0;
      max-height: 0;
      max-width: 0;
      opacity: 0;
      overflow: hidden;
      visibility: hidden;
      width: 0;
    }

    table.main {
      border-radius: 16px;
      border: 1px solid rgba(255, 255, 255, 0.18);
      background-color: #ffffff;
      font-family: 'Noto Sans', 'Segoe UI', 'Helvetica Neue', Arial, sans-serif !important;
      -webkit-font-smoothing: antialiased;
      font-size: 14px;
      line-height: 1.4;
      box-shadow: 0 4px 30px rgba(0, 0, 0, 0.1);
      backdrop-filter: blur(5px);
      -webkit-backdrop-filter: blur(5px);
      margin: 0 0 20px 0;
      width: 100%;
    }

    .wrapper {
      box-sizing: border-box;
      padding: 24px;
    }

    /* Typography */
    p {
      font-size: 15px;
      font-weight: 400;
      line-height: 1.6;
      margin: 0 0 14px;
      color: #1b2a49;
    }

    /* Contact Form Message Styles */
    .message-title {
      color: #06090f;
      font-size: 22px;
      font-weight: 600;
      margin: 0 0 20px;
      line-height: 1.3;
    }

    .message-container {
      background-color: #f8f9fa;
      padding: 20px;
      border-radius: 8px;
      margin-bottom: 24px;
      border-left: 4px solid #ec0867;
    }

    .field-label {
      margin: 0 0 8px;
      font-weight: 600;
      color: #06090f;
      font-size: 14px;
    }

    .field-value {
      margin: 0 0 16px;
      color: #1b2a49;
      font-size: 15px;
      line-height: 1.5;
    }

    .message-text {
      line-height: 1.6;
      margin: 0;
    }

    .signature-separator {
      border: none;
      border-top: 1px solid #e1e5e9;
      margin: 30px 0 20px;
    }

    /* Signature Styles */
    .signature-name {
      font-size: 26px;
      font-weight: 600;
      color: #06090f;
      letter-spacing: 0.6px;
      margin: 4px 0 12px;
    }

    .intro {
      font-size: 15px;
      color: #1b2a49;
      margin: 0 0 8px;
    }

    .role {
      font-size: 15px;
      color: #1b2a49;
      margin: 0 0 6px;
    }

    .location {
      font-size: 14px;
      color: #415476;
      margin: 0 0 20px;
    }

    .signature-layout td {
      vertical-align: top;
    }

    .headshot-cell {
      padding-right: 18px;
    }

    .profile-photo {
      border: none;
      border-radius: 50%;
      height: 110px;
      width: 110px;
      display: block;
    }

    /* Button Styles */
    table.btn {
      box-sizing: border-box;
      margin: 0 0 20px;
      width: auto;
    }

    table.btn table {
      width: auto;
    }

    .btn-primary .btn-padding {
      padding-bottom: 0;
    }

    .btn-primary a {
      border: solid 1px #812f51;
      border-radius: 30px;
      box-sizing: border-box;
      cursor: pointer;
      display: inline-block;
      font-size: 13px;
      font-weight: 600;
      padding: 8px 18px;
      text-decoration: none;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      background-color: #812f51;
      color: #ffffff;
    }

    /* Contact Icons */
    .contact-list {
      margin: 0;
    }

    .contact-list td {
      padding: 0 10px 0 0;
      vertical-align: middle;
    }

    .contact-icon-link {
      display: inline-block;
      line-height: 0;
    }

    .contact-icon-link img {
      display: block;
      width: 24px;
      height: 24px;
    }

    /* Footer */
    .footer {
      margin: 0;
      text-align: center;
    }

    .footer-inner {
      width: 580px;
      max-width: 100%;
      padding: 0;
      margin: 0 auto;
    }

    .footer .content-block {
      padding: 6px 0;
      color: #9a9ea6;
      font-size: 12px;
      text-align: center;
    }

    .footer .apple-link,
    .footer a {
      color: #9a9ea6;
      font-size: 12px;
      text-decoration: none;
      text-align: center;
    }

    .footer a.unsubscribe {
      text-decoration: underline;
    }

    .footer .legal {
      line-height: 1.6;
      max-width: 520px;
      color: #82879b;
      font-size: 11px;
    }

    .text-center {
      text-align: center;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }

    /* Dark Mode Support */
    @media (prefers-color-scheme: dark) {
      body {
        background-color: #0b1320 !important;
        color: #f5f7ff !important;
      }

      table.body {
        background-color: #0b1320 !important;
      }

      table.main {
        background-color: rgba(25, 35, 58, 0.85) !important;
        border-color: rgba(255, 255, 255, 0.18) !important;
        color: #f5f7ff !important;
      }

      p,
      .signature-name,
      .role,
      .location,
      .message-title,
      .field-label,
      .field-value,
      .intro {
        color: #f5f7ff !important;
      }

      .message-container {
        background-color: rgba(40, 50, 73, 0.6) !important;
      }

      .footer a {
        color: #9fd3ff !important;
      }

      .footer .content-block,
      .footer .legal {
        color: #ccd3e5 !important;
      }

      .btn-primary .btn-primary-cell,
      .btn-primary .btn-primary-cell a {
        background-color: #88244e !important;
        border-color: #88244e !important;
      }
    }
    ";
    }

    private static bool IsValidEmail(string? email)
    {
      if (string.IsNullOrWhiteSpace(email)) return false;
      try
      {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
      }
      catch
      {
        return false;
      }
    }
  }
}
