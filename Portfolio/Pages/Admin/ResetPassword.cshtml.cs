using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portfolio.Pages.Admin
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        public ResetPasswordModel(UserManager<IdentityUser> userManager) { _userManager = userManager; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        [BindProperty]
        public InputModel Input { get; set; } = new();
        public class InputModel
        {
            public string UserId { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
            [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
        }
        public void OnGet(string userId, string token)
        {
            Input.UserId = userId; Input.Token = token;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { Error = "Invalid input."; return Page(); }
            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null) { Error = "Invalid user."; return Page(); }
            var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.Password);
            if (result.Succeeded) { Message = "Password reset successful."; return Page(); }
            Error = string.Join("; ", result.Errors.Select(e => e.Description));
            return Page();
        }
    }
}
