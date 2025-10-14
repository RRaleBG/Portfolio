using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Portfolio.Pages.Admin
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        public ConfirmEmailModel(UserManager<IdentityUser> userManager) { _userManager = userManager; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public async Task OnGet(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                Error = "Invalid user.";
                return;
            }
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
            {
                TempData["ToastMessage"] = result.Succeeded ? "Thank you for confirming your email." : "Error confirming your email.";
                TempData["ToastType"] = "success";
            }

            else
                Error = string.Join("; ", result.Errors.Select(e => e.Description));
        }
    }
}
