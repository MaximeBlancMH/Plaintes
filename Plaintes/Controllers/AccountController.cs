using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Plaintes.Models;
using Plaintes.Services;
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
        private readonly IMemoryCache _cache;
        private static readonly string[] Droits = { "R", "W", "A", "S" };
        private string? MonDroit => User.FindFirst(ClaimTypes.Role)?.Value?.Trim().ToUpperInvariant();

        /// <summary>
        /// Fonction qui indique si l'utilisateur connecté peut gérer ce droit : il ne doit pas dépasser le sien.
        /// </summary>
        /// <param name="droit">Droit à comparer</param>
        private bool PeutGerer(string? droit)
        {
            var mien = Array.IndexOf(Droits, MonDroit);
            var cible = string.IsNullOrWhiteSpace(droit) ? 0 : Array.IndexOf(Droits, droit.Trim().ToUpperInvariant());
            return mien >= 0 && cible >= 0 && cible <= mien;
        }

        public AccountController(IConfiguration config, ILogger<AccountController> logger, IUtilisateurSrv utilisateurSrv, IMemoryCache cache)
        {
            _config = config;
            _logger = logger;
            _utilisateurSrv = utilisateurSrv;
            _cache = cache;
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
                TempData["Erreur"] = "Vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(AllAccounts));
            }

            if (!PeutGerer(user.Access))
            {
                TempData["Erreur"] = "Vous ne pouvez pas supprimer un utilisateur dont le rôle est supérieur au vôtre.";
                return RedirectToAction(nameof(AllAccounts));
            }

            await _utilisateurSrv.DeleteUser(id);
            ComptesClaims.OublierSession(_cache, user.UserEmail);      // sa session est coupée tout de suite, sans attendre

            return RedirectToAction(nameof(AllAccounts));
        }

        /// <summary>
        /// Action de type POST qui va contrôler les valeurs reçues puis ajouter un utilisateur en base de données
        /// </summary>
        /// <param name="email">Email du nouvel utilisateur</param>
        /// <param name="role">Role du nouvel utilisateur</param>
        /// <param name="dateFin">Date de fin d'accès, facultative (vide = sans limite)</param>
        /// <returns></returns>
        [HttpPost("AddAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> AddAccount(string email, string role, DateOnly? dateFin = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                TempData["Erreur"] = "Tous les champs sont obligatoires.";
                return RedirectToAction(nameof(AllAccounts));
            }

            if (!Droits.Contains(role))
            {
                TempData["Erreur"] = "Le rôle spécifié n'est pas valide.";
                return RedirectToAction(nameof(AllAccounts));
            }

            if (!PeutGerer(role))
            {
                TempData["Erreur"] = "Vous ne pouvez pas créer un utilisateur avec un rôle supérieur au vôtre.";
                return RedirectToAction(nameof(AllAccounts));
            }

            // On vérifie que l'email n'est pas déjà pris
            var existingUsers = await _utilisateurSrv.GetAllUsers();
            if (existingUsers.Any(u => string.Equals(u.UserEmail, email, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Erreur"] = "Un utilisateur avec cet email existe déjà.";
                return RedirectToAction(nameof(AllAccounts));
            }

            await _utilisateurSrv.AddUser(email, role, dateFin);
            ComptesClaims.OublierSession(_cache, email);
            return RedirectToAction(nameof(AllAccounts));
        }

        /// <summary>
        /// Action de type POST qui va contrôler les nouvelles valeurs puis modifier l'utilisateur
        /// </summary>
        /// <param name="id">Id de l'utilisateur à modifier</param>
        /// <param name="name">Nouveau nom de l'utilisateur (facultatif)</param>
        /// <param name="email">Nouvel email de l'utilisateur</param>
        /// <param name="role">Nouveau rôle de l'utilisateur</param>
        /// <param name="dateFin">Nouvelle date de fin d'accès (vide = sans limite)</param>
        /// <returns></returns>
        [HttpPost("EditAccount")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        public async Task<IActionResult> EditAccount(int id, string name, string email, string role, DateOnly? dateFin = null)
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

            if (!Droits.Contains(role))
            {
                TempData["Erreur"] = "Le rôle spécifié n'est pas valide.";
                return RedirectToAction(nameof(AllAccounts));
            }

            // Sur son propre compte, seul un super administrateur peut changer son e-mail, son rôle ou sa date de fin (le nom reste modifiable)
            var estMoi = !string.IsNullOrEmpty(user.UserEmail)
                && string.Equals(user.UserEmail, User.FindFirstValue(ClaimTypes.Email), StringComparison.OrdinalIgnoreCase);
            if (estMoi && MonDroit != "S"
                && (!string.Equals(email.Trim(), user.UserEmail!.Trim(), StringComparison.OrdinalIgnoreCase)
                    || role != user.Access?.Trim().ToUpperInvariant()
                    || dateFin != user.DateFinu))
            {
                TempData["Erreur"] = "Seul un super administrateur peut modifier son propre e-mail, son propre rôle ou sa propre date de fin.";
                return RedirectToAction(nameof(AllAccounts));
            }

            // On ne modifie pas quelqu'un qui a plus de droits que soi, et on ne donne pas plus de droits que les siens
            if (!PeutGerer(user.Access))
            {
                TempData["Erreur"] = "Vous ne pouvez pas modifier un utilisateur dont le rôle est supérieur au vôtre.";
                return RedirectToAction(nameof(AllAccounts));
            }
            if (!PeutGerer(role))
            {
                TempData["Erreur"] = "Vous ne pouvez pas donner un rôle supérieur au vôtre.";
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
            await _utilisateurSrv.UpdateUser(id, nom, email, role, dateFin);
            // Le nouveau droit s'applique dès la page suivante (y compris à soi-même : boutons, menus)
            ComptesClaims.OublierSession(_cache, user.UserEmail);
            ComptesClaims.OublierSession(_cache, email);
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
