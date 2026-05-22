using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using PCKManagementSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace PCKManagementSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<User> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel(); // <-- ÈÍÈÖÈÀËÈÇÀÖÈß

        public string? ReturnUrl { get; set; } // <-- ÑÄÅËÀË NULLABLE (âîïðîñ)

        [TempData]
        public string? ErrorMessage { get; set; } // <-- ÑÄÅËÀË NULLABLE

        public class InputModel
        {
            [Required(ErrorMessage = "Email îáÿçàòåëåí")]
            [EmailAddress(ErrorMessage = "Ââåäèòå êîððåêòíûé email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty; // <-- ÈÍÈÖÈÀËÈÇÀÖÈß

            [Required(ErrorMessage = "Ïàðîëü îáÿçàòåëåí")]
            [DataType(DataType.Password)]
            [Display(Name = "Ïàðîëü")]
            public string Password { get; set; } = string.Empty; // <-- ÈÍÈÖÈÀËÈÇÀÖÈß

            [Display(Name = "Çàïîìíèòü ìåíÿ?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null) // <-- NULLABLE ÏÀÐÀÌÅÒÐ
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null) // <-- NULLABLE ÏÀÐÀÌÅÒÐ
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Ïîëüçîâàòåëü âîøåë â ñèñòåìó.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Àêêàóíò çàáëîêèðîâàí.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Íåóäà÷íàÿ ïîïûòêà âõîäà.");
                    return Page();
                }
            }

            return Page();
        }
    }
}