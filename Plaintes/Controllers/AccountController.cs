using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sustainsys.Saml2.AspNetCore2;

namespace Plaintes.Controllers
{
    [Route("Account")]
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config)
        {
            _config = config;
        }
        [HttpGet("Login")]
        public IActionResult Login(string? returnURl = "/")
        {
            if (!Url.IsLocalUrl(returnURl)) returnURl = "/";
            return Challenge(new AuthenticationProperties { RedirectUri = returnURl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = Url.Action("Deconnecte", "Account") },
                CookieAuthenticationDefaults.AuthenticationScheme,
                Saml2Defaults.Scheme);
        }

        [HttpGet("Deconnecte")]
        public IActionResult Deconnecte() => View();

        [HttpGet("AccessDenied")]
        public IActionResult AccessDenied() => RedirectToAction(nameof(Login));
    }
}
