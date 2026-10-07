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

        /// <summary>
        /// Action qui déclenche la connexion Microsoft
        /// </summary>
        /// <param name="returnURl">Où doit renvoyer l'action à la fin de son traitement</param>
        /// <returns></returns>
        [HttpGet("Login")]
        public IActionResult Login(string? returnURl = "/")
        {
            if (!Url.IsLocalUrl(returnURl)) returnURl = "/";
            return Challenge(new AuthenticationProperties { RedirectUri = returnURl });
        }

        /// <summary>
        /// Action de type POST qui déconnecte l'utilisateur de l'application. 
        /// Dans notre cas, on supprime la session de l'application avec celle de Microsoft en plus
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Action qui récupère la liste des utilisateurs de l'application puis renvoie vers la vue associée
        /// </summary>
        /// <returns></returns>
        [HttpGet("AllAccounts")]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> AllAccounts()
        {
            var users = await _utilisateurSrv.GetAllUsers();
            return View(users);
        }

        /// <summary>
        /// Action de type POST qui va effectuer les contrôles puis les actions pour "supprimer" un 
        /// utilisateur en base de données (on met la date d'aujourd'hui à DateFinu pour préciser qu'il était valide jusqu'à aujourd'hui)
        /// </summary>
        /// <param name="id">Id de l'utilisateur à supprimer</param>
        /// <returns></returns>
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

        /// <summary>
        /// Action de type POST qui va contrôler les valeurs reçues puis ajouter un utilisateur en base de données
        /// </summary>
        /// <param name="email">Email du nouvel utilisateur</param>
        /// <param name="role">Role du nouvel utilisateur</param>
        /// <returns></returns>
        [HttpPost("AddAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> AddAccount(string email, string role)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
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

            await _utilisateurSrv.AddUser(email, role);
            return RedirectToAction(nameof(AllAccounts));
        }

        /// <summary>
        /// Action de type POST qui va contrôler les nouvelles valeurs puis modifier l'utilisateur
        /// </summary>
        /// <param name="id">Id de l'utilisateur à modifier</param>
        /// <param name="name">Nouveau nom de l'utilisateur (facultatif)</param>
        /// <param name="email">Nouvel email de l'utilisateur</param>
        /// <param name="role">Nouveau rôle de l'utilisateur</param>
        /// <returns></returns>
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

            // Mêmes contrôles que pour l'ajout. Le nom est facultatif : il vient de Microsoft à la première connexion de l'utilisateur.
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
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

            // Nom laissé vide : on garde celui qui existe déjà (ou l'absence de nom, tant que l'utilisateur ne s'est pas connecté).
            var nom = string.IsNullOrWhiteSpace(name) ? user.NomUser : name.Trim();
            await _utilisateurSrv.UpdateUser(id, nom, email, role);
            return RedirectToAction(nameof(AllAccounts));
        }

        /// <summary>
        /// Simple action qui renvoie vers la vue affichant la confirmation de déconnexion
        /// </summary>
        /// <returns></returns>
        [HttpGet("Deconnecte")]
        public IActionResult Deconnecte() => View();

        /// <summary>
        /// Action renvoyant la page d'accès refusé
        /// </summary>
        /// <returns></returns>
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
