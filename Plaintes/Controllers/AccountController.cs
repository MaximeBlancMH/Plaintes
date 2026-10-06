using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plaintes.Models;
using Plaintes.Services.IServices;
using Sustainsys.Saml2.AspNetCore2;
using System.Security.Claims;

namespace Plaintes.Controllers
{
    [Route("Account")]
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AccountController> _logger;
        private readonly IUtilisateurSrv _utilisateurSrv;

        public AccountController(IConfiguration config, ILogger<AccountController> logger, IUtilisateurSrv utilisateurSrv)
        {
            _config = config;
            _logger = logger;
            _utilisateurSrv = utilisateurSrv;
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

        [HttpGet("AllAccounts")]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> AllAccounts()
        {
            var users = await _utilisateurSrv.GetAllUsers();
            return View(users);
        }

        [HttpPost("DeleteAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var user = await _utilisateurSrv.GetUserById(id);
            if (user == null)
            {
                return NotFound();
            }
                
            if(user.UserEmail == User.FindFirstValue(ClaimTypes.Email))
            {
                // Les erreurs passent par TempData (ModelState ne survit pas à la redirection) ; la vue AllAccounts les affiche.
                TempData["Erreur"] = "Vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(AllAccounts));
            }

            await _utilisateurSrv.DeleteUser(id);

            return RedirectToAction(nameof(AllAccounts));
        }

        [HttpPost("AddAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> AddAccount(string name, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                TempData["Erreur"] = "Tous les champs sont obligatoires.";
                return RedirectToAction(nameof(AllAccounts));
            }

            if (!new[] { "R", "W", "A", "S" }.Contains(role))
            {
                TempData["Erreur"] = "Le rôle spécifié n'est pas valide.";
                return RedirectToAction(nameof(AllAccounts));
            }

            // On vérifie que l'email n'est pas déjà pris
            var existingUsers = await _utilisateurSrv.GetAllUsers();
            if (existingUsers.Any(u => string.Equals(u.UserEmail, email, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Erreur"] = "Un utilisateur avec cet email existe déjà.";
                return RedirectToAction(nameof(AllAccounts));
            }

            await _utilisateurSrv.AddUser(name, email, role);
            return RedirectToAction(nameof(AllAccounts));
        }

        [HttpPost("EditAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> EditAccount(int id, string name, string email, string role)
        {
            var user = await _utilisateurSrv.GetUserById(id);
            if (user == null)
            {
                return NotFound();
            }

            // Mêmes contrôles que pour l'ajout.
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                TempData["Erreur"] = "Tous les champs sont obligatoires.";
                return RedirectToAction(nameof(AllAccounts));
            }

            if (!new[] { "R", "W", "A", "S" }.Contains(role))
            {
                TempData["Erreur"] = "Le rôle spécifié n'est pas valide.";
                return RedirectToAction(nameof(AllAccounts));
            }

            // L'e-mail ne doit pas déjà appartenir à un autre utilisateur (le sien, inchangé, est bien sûr accepté).
            var existingUsers = await _utilisateurSrv.GetAllUsers();
            if (existingUsers.Any(u => u.IdUser != id && string.Equals(u.UserEmail, email, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Erreur"] = "Un utilisateur avec cet email existe déjà.";
                return RedirectToAction(nameof(AllAccounts));
            }

            await _utilisateurSrv.UpdateUser(id, name, email, role);
            return RedirectToAction(nameof(AllAccounts));
        }

        [HttpGet("Deconnecte")]
        public IActionResult Deconnecte() => View();

        // Page « vous n'avez pas accès à cette page » (droits insuffisants, compte qui n'est plus actif).
        // Visible sans être connecté ; la réponse porte le code 403 (accès interdit).
        [HttpGet("AccesRefuse")]
        [AllowAnonymous]
        public IActionResult AccesRefuse()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        [HttpGet("AccessDenied")]
        public IActionResult AccessDenied() => RedirectToAction(nameof(AccesRefuse));
    }
}
