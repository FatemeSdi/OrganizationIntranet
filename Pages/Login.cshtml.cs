using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrganizationIntranet.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "رمز عبور الزامی است")]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return new JsonResult(new { success = false, message = "نام کاربری و رمز عبور را وارد کنید" });
            }

            return new JsonResult(new { success = true, redirectUrl = Url.Content("~/portal.html") });
        }
    }
}
