using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Controllers;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Reflection;

namespace Plaintes.Tests
{
    public class AccountControllerTests
    {
        internal static AccountController CreerController(IUtilisateurSrv? utilisateurs = null)
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new AccountController(new ConfigurationBuilder().Build(), NullLogger<AccountController>.Instance, utilisateurs ?? new FauxUtilisateurSrv())
            {
                ControllerContext = new ControllerContext(actionContext),
                Url = new UrlHelper(actionContext),
                TempData = new TempDataDictionary(httpContext, new TempDataVide())
            };
        }

        [Fact]
        public void Login_UrlLocale_EstConserveeCommeRedirection()
        {
            var result = Assert.IsType<ChallengeResult>(CreerController().Login("/Dossiers?page=2"));

            Assert.Equal("/Dossiers?page=2", result.Properties!.RedirectUri);
        }

        [Theory]
        [InlineData("https://site-malveillant.fr")]
        [InlineData("//site-malveillant.fr")]
        [InlineData("http://localhost/autre")]
        public void Login_UrlExterne_EstRemplaceeParLaRacine(string returnUrl)
        {
            var result = Assert.IsType<ChallengeResult>(CreerController().Login(returnUrl));

            Assert.Equal("/", result.Properties!.RedirectUri);
        }

        [Fact]
        public void Deconnecte_RenvoieLaVue()
        {
            Assert.IsType<ViewResult>(CreerController().Deconnecte());
        }

        [Fact]
        public void AccessDenied_RedirigeVersLaPageAccesRefuse()
        {
            var result = Assert.IsType<RedirectToActionResult>(CreerController().AccessDenied());

            Assert.Equal("AccesRefuse", result.ActionName);
        }

        /// <summary>TempData sans stockage : suffit pour lire ce que le contrôleur y a écrit pendant le test.</summary>
        internal sealed class TempDataVide : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        // ---------- Gestion des utilisateurs ----------

        /// <summary>Faux service : garde les utilisateurs en mémoire et note les appels reçus.</summary>
        internal sealed class FauxUtilisateurSrv : IUtilisateurSrv
        {
            public List<Utilisateur> Utilisateurs { get; } = new();
            public List<(string Email, string Role)> Ajouts { get; } = new();
            public List<int> Suppressions { get; } = new();
            public List<(int Id, string? Nom, string Email, string Role)> Modifications { get; } = new();

            public Task UpdateNameUser(int idUser, string newName) => Task.CompletedTask;
            public Task<List<Utilisateur>> GetAllUsers() => Task.FromResult(Utilisateurs);
            public Task<Utilisateur?> GetUserById(int idUser) => Task.FromResult(Utilisateurs.FirstOrDefault(u => u.IdUser == idUser));
            public Task DeleteUser(int idUser) { Suppressions.Add(idUser); return Task.CompletedTask; }
            public Task AddUser(string email, string role) { Ajouts.Add((email, role)); return Task.CompletedTask; }
            public Task UpdateUser(int idUser, string? newName, string newEmail, string newRole) { Modifications.Add((idUser, newName, newEmail, newRole)); return Task.CompletedTask; }
        }

        [Fact]
        public async Task AllAccounts_AfficheLaListeDesUtilisateurs()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1 }, new Utilisateur { IdUser = 2 } } };

            var view = Assert.IsType<ViewResult>(await CreerController(srv).AllAccounts());

            var model = Assert.IsAssignableFrom<List<Utilisateur>>(view.Model);
            Assert.Equal(new[] { 1, 2 }, model.Select(u => u.IdUser));
        }

        [Theory]
        [InlineData("R")]
        [InlineData("W")]
        [InlineData("A")]
        [InlineData("S")]
        public async Task AddAccount_Valide_AjouteLUtilisateurEtRedirigeVersLaListe(string role)
        {
            var srv = new FauxUtilisateurSrv();

            var result = Assert.IsType<RedirectToActionResult>(await CreerController(srv).AddAccount("dupont@test.fr", role));

            Assert.Equal("AllAccounts", result.ActionName);
            Assert.Equal(("dupont@test.fr", role), Assert.Single(srv.Ajouts));
        }

        [Theory]
        [InlineData("", "R")]
        [InlineData("  ", "R")]
        [InlineData(null, "R")]
        [InlineData("d@test.fr", "")]
        [InlineData("d@test.fr", null)]
        public async Task AddAccount_ChampManquant_NAjouteRien(string? email, string? role)
        {
            var srv = new FauxUtilisateurSrv();

            var result = await CreerController(srv).AddAccount(email!, role!);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(srv.Ajouts);
        }

        [Theory]
        [InlineData("X")]
        [InlineData("admin")]
        [InlineData("r")]
        public async Task AddAccount_DroitInvalide_NAjouteRien(string role)
        {
            var srv = new FauxUtilisateurSrv();

            await CreerController(srv).AddAccount("d@test.fr", role);

            Assert.Empty(srv.Ajouts);
        }

        [Fact]
        public async Task AddAccount_EmailDejaUtilise_SansTenirCompteDeLaCasse_NAjouteRien()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "Dupont@Test.fr" } } };

            await CreerController(srv).AddAccount("dupont@test.FR", "R");

            Assert.Empty(srv.Ajouts);
        }

        [Fact]
        public async Task AddAccount_UnUtilisateurExistantSansEmail_NeFaitPasPlanter()
        {
            // UserEmail est facultatif en base : la recherche de doublon ne doit pas échouer sur un e-mail absent.
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = null } } };

            await CreerController(srv).AddAccount("dupont@test.fr", "R");

            Assert.Single(srv.Ajouts);
        }

        [Fact]
        public async Task EditAccount_TransmetLesValeursAuService_EtRedirigeVersLaListe()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 7, UserEmail = "ancien@test.fr" } } };

            var result = Assert.IsType<RedirectToActionResult>(await CreerController(srv).EditAccount(7, "Nouveau", "nouveau@test.fr", "A"));

            Assert.Equal("AllAccounts", result.ActionName);
            Assert.Equal((7, "Nouveau", "nouveau@test.fr", "A"), Assert.Single(srv.Modifications));
        }

        [Fact]
        public async Task DeleteAccount_SupprimeLUtilisateurEtRedirigeVersLaListe()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 5, UserEmail = "autre@test.fr" } } };

            var result = Assert.IsType<RedirectToActionResult>(await CreerController(srv).DeleteAccount(5));

            Assert.Equal("AllAccounts", result.ActionName);
            Assert.Equal(5, Assert.Single(srv.Suppressions));
        }

        // ---------- Messages d'erreur affichés par la page (TempData["Erreur"]) ----------

        private static string? Erreur(AccountController c) => c.TempData["Erreur"] as string;

        [Fact]
        public async Task AddAccount_Valide_NAfficheAucuneErreur()
        {
            var c = CreerController();

            await c.AddAccount("dupont@test.fr", "R");

            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task AddAccount_ChampManquant_AfficheUneErreur()
        {
            var c = CreerController();

            await c.AddAccount("", "R");

            Assert.Equal("Tous les champs sont obligatoires.", Erreur(c));
        }

        [Fact]
        public async Task AddAccount_DroitInvalide_AfficheUneErreur()
        {
            var c = CreerController();

            await c.AddAccount("dupont@test.fr", "X");

            Assert.Contains("rôle", Erreur(c));
        }

        [Fact]
        public async Task AddAccount_EmailDejaUtilise_AfficheUneErreur()
        {
            var c = CreerController(new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "dupont@test.fr" } } });

            await c.AddAccount("dupont@test.fr", "R");

            Assert.Contains("existe déjà", Erreur(c));
        }

        private static AccountController CreerControllerConnecte(string email, FauxUtilisateurSrv srv)
        {
            var c = CreerController(srv);
            c.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email) }, "test"));
            return c;
        }

        [Fact]
        public async Task DeleteAccount_SonPropreCompte_EstRefuse_AvecUneErreur()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "moi@test.fr" } } };
            var c = CreerControllerConnecte("moi@test.fr", srv);

            var result = Assert.IsType<RedirectToActionResult>(await c.DeleteAccount(1));

            Assert.Equal("AllAccounts", result.ActionName);
            Assert.Empty(srv.Suppressions);
            Assert.Equal("Vous ne pouvez pas supprimer votre propre compte.", Erreur(c));
        }

        [Fact]
        public async Task DeleteAccount_AutreUtilisateur_EstSupprime_SansErreur()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 2, UserEmail = "autre@test.fr" } } };
            var c = CreerControllerConnecte("moi@test.fr", srv);

            await c.DeleteAccount(2);

            Assert.Equal(new[] { 2 }, srv.Suppressions);
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task DeleteAccount_UtilisateurInconnu_RenvoieNotFound()
        {
            var srv = new FauxUtilisateurSrv();

            Assert.IsType<NotFoundResult>(await CreerController(srv).DeleteAccount(99));
            Assert.Empty(srv.Suppressions);
        }

        // ---------- EditAccount : contrôles et messages ----------

        private static FauxUtilisateurSrv DeuxUtilisateurs() => new()
        {
            Utilisateurs =
            {
                new Utilisateur { IdUser = 1, NomUser = "Un", UserEmail = "un@test.fr", Access = "R" },
                new Utilisateur { IdUser = 2, NomUser = "Deux", UserEmail = "deux@test.fr", Access = "R" }
            }
        };

        [Fact]
        public async Task EditAccount_Valide_NAfficheAucuneErreur()
        {
            var c = CreerController(DeuxUtilisateurs());

            await c.EditAccount(1, "Nouveau", "nouveau@test.fr", "A");

            Assert.Null(Erreur(c));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task EditAccount_NomVide_GardeLeNomExistant_SansErreur(string? nom)
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv);

            await c.EditAccount(1, nom!, "un@test.fr", "W");

            Assert.Equal((1, "Un", "un@test.fr", "W"), Assert.Single(srv.Modifications));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_UtilisateurSansNom_PeutEtreModifie_EtResteSansNom()
        {
            // Un utilisateur ajouté n'a pas de nom avant sa première connexion : modifier ses droits ne doit pas exiger un nom.
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 5, NomUser = null, UserEmail = "nouveau@test.fr", Access = "R" } } };
            var c = CreerController(srv);

            await c.EditAccount(5, "", "nouveau@test.fr", "W");

            Assert.Equal((5, (string?)null, "nouveau@test.fr", "W"), Assert.Single(srv.Modifications));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_NomRenseigne_EstEnregistre_SansEspacesAutour()
        {
            var srv = DeuxUtilisateurs();

            await CreerController(srv).EditAccount(1, "  Nouveau nom  ", "un@test.fr", "R");

            Assert.Equal("Nouveau nom", Assert.Single(srv.Modifications).Nom);
        }

        [Fact]
        public async Task EditAccount_GardeLEmailActuel_EstAccepte()
        {
            // L'e-mail de l'utilisateur modifié n'est pas un « doublon » de lui-même.
            var srv = DeuxUtilisateurs();

            await CreerController(srv).EditAccount(1, "Un modifié", "UN@test.fr", "W");

            Assert.Single(srv.Modifications);
        }

        [Fact]
        public async Task EditAccount_EmailDUnAutreUtilisateur_EstRefuse_AvecUneErreur()
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv);

            await c.EditAccount(1, "Un", "DEUX@test.fr", "R");

            Assert.Empty(srv.Modifications);
            Assert.Contains("existe déjà", Erreur(c));
        }

        [Theory]
        [InlineData("Nom", " ", "R")]
        [InlineData("Nom", "a@test.fr", "")]
        [InlineData(null, "", "R")]
        [InlineData("", "a@test.fr", null)]
        public async Task EditAccount_ChampManquant_NeModifieRien_EtAfficheUneErreur(string? nom, string? email, string? role)
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv);

            var result = await c.EditAccount(1, nom!, email!, role!);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(srv.Modifications);
            Assert.Equal("Tous les champs sont obligatoires.", Erreur(c));
        }

        [Theory]
        [InlineData("X")]
        [InlineData("admin")]
        [InlineData("a")]
        public async Task EditAccount_DroitInvalide_NeModifieRien_EtAfficheUneErreur(string role)
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv);

            await c.EditAccount(1, "Un", "un@test.fr", role);

            Assert.Empty(srv.Modifications);
            Assert.Contains("rôle", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_UtilisateurInconnu_RenvoieNotFound()
        {
            var srv = DeuxUtilisateurs();

            Assert.IsType<NotFoundResult>(await CreerController(srv).EditAccount(99, "Nom", "a@test.fr", "R"));
            Assert.Empty(srv.Modifications);
        }

        // ---------- Page « accès refusé » ----------

        [Fact]
        public void AccesRefuse_AfficheLaPage_AvecLeCode403()
        {
            var c = CreerController();

            Assert.IsType<ViewResult>(c.AccesRefuse());
            Assert.Equal(StatusCodes.Status403Forbidden, c.Response.StatusCode);
        }

        [Fact]
        public void AccesRefuse_EstVisibleSansEtreConnecte()
        {
            // Un compte désactivé ne peut plus se connecter : la page doit s'ouvrir quand même.
            var methode = typeof(AccountController).GetMethod(nameof(AccountController.AccesRefuse))!;

            Assert.NotNull(methode.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(methode.GetCustomAttribute<AuthorizeAttribute>());
        }

        // ---------- Sécurité de la gestion des utilisateurs ----------

        [Fact]
        public void LeControleur_NEstPasOuvertAToutLeMonde()
        {
            // Un [AllowAnonymous] sur la classe annulerait tous les [Authorize] des actions.
            Assert.Null(typeof(AccountController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Theory]
        [InlineData(nameof(AccountController.AllAccounts))]
        [InlineData(nameof(AccountController.AddAccount))]
        [InlineData(nameof(AccountController.EditAccount))]
        [InlineData(nameof(AccountController.DeleteAccount))]
        public void LesActionsDeGestion_SontReserveesAuxAdministrateurs(string action)
        {
            var methode = typeof(AccountController).GetMethod(action)!;

            var autorisation = methode.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(autorisation);
            // Droits stockés dans Utilisateur.Access : A = administrateur, S = super administrateur.
            Assert.Equal("A,S", autorisation!.Roles);
            Assert.Null(methode.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Theory]
        [InlineData(nameof(AccountController.AddAccount))]
        [InlineData(nameof(AccountController.EditAccount))]
        [InlineData(nameof(AccountController.DeleteAccount))]
        public void LesActionsQuiModifient_ExigentLeJetonAntiFalsification(string action)
        {
            var methode = typeof(AccountController).GetMethod(action)!;

            Assert.NotNull(methode.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(methode.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        [Fact]
        public void AddAccount_NePrendPasDeNom_LeNomVientDeMicrosoftALaPremiereConnexion()
        {
            var noms = typeof(AccountController).GetMethod(nameof(AccountController.AddAccount))!.GetParameters().Select(p => p.Name);

            Assert.Equal(new[] { "email", "role" }, noms);
        }

        [Fact]
        public void EditAccount_RecoitLesChampsEnvoyesParLeFormulaire()
        {
            // Les champs du formulaire (Views/Account/AllAccounts.cshtml) s'appellent id, name, email et role :
            // avec d'autres noms, le formulaire arriverait vide et effacerait l'utilisateur.
            var noms = typeof(AccountController).GetMethod(nameof(AccountController.EditAccount))!.GetParameters().Select(p => p.Name);

            Assert.Equal(new[] { "id", "name", "email", "role" }, noms);
        }
    }
}
