using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Portfolio.Pages.Admin
{
    [AllowAnonymous]
    public class LoginModel(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager) : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager = signInManager;
        private readonly UserManager<IdentityUser> _userManager = userManager;

        [BindProperty]
        public LoginInput Input { get; set; } = new();
        [BindProperty]
        public string? ReturnUrl { get; set; }
        public string? Error { get; set; }

        public class LoginInput
        {
            public string? Email { get; set; }
            public string? Password { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("/Dashboard");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Input?.Email ?? Url.Content("/Dashboard");

            if (string.IsNullOrWhiteSpace(Input?.Email) || string.IsNullOrWhiteSpace(Input.Password))
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = "Email and password are required.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = "Invalid credentials.";
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(user, Input.Password, true, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                return LocalRedirect(ReturnUrl ?? "/Dashboard");
            }

            TempData["ToastType"] = "error";
            TempData["ToastMessage"] = "Invalid credentials.";
            return Page();
        }
    }
}
