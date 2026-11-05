using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
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
        /// <summary>Contrôleur de test. L'utilisateur connecté a le droit donné (S par défaut, null = aucun droit).</summary>
        internal static AccountController CreerController(IUtilisateurSrv? utilisateurs = null, string? droit = "S", IMemoryCache? cache = null)
        {
            var httpContext = new DefaultHttpContext();
            if (droit != null)
                httpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, droit) }, "test"));
            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new AccountController(new ConfigurationBuilder().Build(), NullLogger<AccountController>.Instance, utilisateurs ?? new FauxUtilisateurSrv(),
                cache ?? new MemoryCache(new MemoryCacheOptions()))
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
            public List<DateOnly?> DatesFinAjout { get; } = new();
            public List<DateOnly?> DatesFinModification { get; } = new();
            public Task AddUser(string email, string role, DateOnly? dateFin = null) { Ajouts.Add((email, role)); DatesFinAjout.Add(dateFin); return Task.CompletedTask; }
            public Task UpdateUser(int idUser, string? newName, string newEmail, string newRole, DateOnly? newDateFin) { Modifications.Add((idUser, newName, newEmail, newRole)); DatesFinModification.Add(newDateFin); return Task.CompletedTask; }
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

        private static AccountController CreerControllerConnecte(string email, FauxUtilisateurSrv srv, string droit = "S")
        {
            var c = CreerController(srv);
            c.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, droit)
                }, "test"));
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

        // ---------- Hiérarchie des droits : on ne gère pas plus haut que soi ----------

        [Theory]
        [InlineData("A", "R")]
        [InlineData("A", "W")]
        [InlineData("A", "A")]      // le même niveau est accepté
        [InlineData("S", "S")]
        public async Task AddAccount_DroitInferieurOuEgalAuSien_EstAccepte(string monDroit, string role)
        {
            var srv = new FauxUtilisateurSrv();
            var c = CreerController(srv, monDroit);

            await c.AddAccount("nouveau@test.fr", role);

            Assert.Equal(("nouveau@test.fr", role), Assert.Single(srv.Ajouts));
            Assert.Null(Erreur(c));
        }

        [Theory]
        [InlineData("A", "S")]
        [InlineData("W", "A")]
        [InlineData("R", "W")]
        public async Task AddAccount_DroitSuperieurAuSien_EstRefuse_AvecUneErreur(string monDroit, string role)
        {
            var srv = new FauxUtilisateurSrv();
            var c = CreerController(srv, monDroit);

            var result = Assert.IsType<RedirectToActionResult>(await c.AddAccount("nouveau@test.fr", role));

            Assert.Equal("AllAccounts", result.ActionName);
            Assert.Empty(srv.Ajouts);
            Assert.Equal("Vous ne pouvez pas créer un utilisateur avec un rôle supérieur au vôtre.", Erreur(c));
        }

        [Theory]
        [InlineData(null)]          // pas de droit dans le cookie
        [InlineData("X")]           // droit inconnu
        public async Task AddAccount_SansDroitConnu_RienNEstAccepte(string? monDroit)
        {
            var srv = new FauxUtilisateurSrv();

            await CreerController(srv, monDroit).AddAccount("nouveau@test.fr", "R");

            Assert.Empty(srv.Ajouts);
        }

        [Fact]
        public async Task AddAccount_DroitDuCookieMalFormate_EstNormalise()
        {
            var srv = new FauxUtilisateurSrv();

            await CreerController(srv, " a ").AddAccount("nouveau@test.fr", "W");

            Assert.Single(srv.Ajouts);
        }

        [Fact]
        public async Task EditAccount_UtilisateurAuDroitSuperieur_EstRefuse_AvecUneErreur()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "chef@test.fr", Access = "S" } } };
            var c = CreerController(srv, "A");

            await c.EditAccount(1, "Chef", "chef@test.fr", "R");

            Assert.Empty(srv.Modifications);
            Assert.Equal("Vous ne pouvez pas modifier un utilisateur dont le rôle est supérieur au vôtre.", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_DonnerUnDroitSuperieurAuSien_EstRefuse_AvecUneErreur()
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv, "A");

            await c.EditAccount(1, "Un", "un@test.fr", "S");

            Assert.Empty(srv.Modifications);
            Assert.Equal("Vous ne pouvez pas donner un rôle supérieur au vôtre.", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_AdministrateurPeutPromouvoirJusquASonNiveau()
        {
            var srv = DeuxUtilisateurs();
            var c = CreerController(srv, "A");

            await c.EditAccount(1, "Un", "un@test.fr", "A");

            Assert.Equal((1, "Un", "un@test.fr", "A"), Assert.Single(srv.Modifications));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_DroitEnBaseMalFormate_EstComparéNormalisé()
        {
            // « s » en base est un super administrateur : un administrateur ne doit pas pouvoir le modifier
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "chef@test.fr", Access = " s" } } };

            await CreerController(srv, "A").EditAccount(1, "Chef", "chef@test.fr", "R");

            Assert.Empty(srv.Modifications);
        }

        [Fact]
        public async Task EditAccount_UtilisateurSansDroit_PeutEtreModifie()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "vide@test.fr", Access = null } } };

            await CreerController(srv, "A").EditAccount(1, "", "vide@test.fr", "R");

            Assert.Single(srv.Modifications);
        }

        // ---------- Son propre compte : seul un super administrateur change son e-mail et son rôle ----------

        private static FauxUtilisateurSrv MonCompte(string droit) => new()
        {
            Utilisateurs = { new Utilisateur { IdUser = 1, NomUser = "Moi", UserEmail = "moi@test.fr", Access = droit } }
        };

        [Fact]
        public async Task EditAccount_SonPropreCompte_AdministrateurNePeutPasChangerSonEmail()
        {
            var srv = MonCompte("A");
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Moi", "nouveau@test.fr", "A");

            Assert.Empty(srv.Modifications);
            Assert.Equal("Seul un super administrateur peut modifier son propre e-mail, son propre rôle ou sa propre date de fin.", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_AdministrateurNePeutPasChangerSonRole()
        {
            var srv = MonCompte("A");
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Moi", "moi@test.fr", "R");

            Assert.Empty(srv.Modifications);
            Assert.Equal("Seul un super administrateur peut modifier son propre e-mail, son propre rôle ou sa propre date de fin.", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_AdministrateurPeutChangerSonNom()
        {
            var srv = MonCompte("A");
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Nouveau nom", "MOI@test.fr", "A");     // même e-mail, à la casse près

            Assert.Equal((1, "Nouveau nom", "MOI@test.fr", "A"), Assert.Single(srv.Modifications));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_SuperAdministrateurPeutToutChanger()
        {
            var srv = MonCompte("S");
            var c = CreerControllerConnecte("moi@test.fr", srv, "S");

            await c.EditAccount(1, "Moi", "nouveau@test.fr", "A");

            Assert.Equal((1, "Moi", "nouveau@test.fr", "A"), Assert.Single(srv.Modifications));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_AdministrateurNePeutPasChangerSaDateDeFin()
        {
            var srv = MonCompte("A");
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Moi", "moi@test.fr", "A", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));

            Assert.Empty(srv.Modifications);
            Assert.Equal("Seul un super administrateur peut modifier son propre e-mail, son propre rôle ou sa propre date de fin.", Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_AdministrateurGardantSaDateDeFin_EstAccepte()
        {
            var fin = DateOnly.FromDateTime(DateTime.Today.AddMonths(6));
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, NomUser = "Moi", UserEmail = "moi@test.fr", Access = "A", DateFinu = fin } } };
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Nouveau nom", "moi@test.fr", "A", fin);

            Assert.Single(srv.Modifications);
            Assert.Equal(fin, Assert.Single(srv.DatesFinModification));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_SuperAdministrateurPeutChangerSaDateDeFin()
        {
            var fin = DateOnly.FromDateTime(DateTime.Today.AddYears(1));
            var srv = MonCompte("S");
            var c = CreerControllerConnecte("moi@test.fr", srv, "S");

            await c.EditAccount(1, "Moi", "moi@test.fr", "S", fin);

            Assert.Equal(fin, Assert.Single(srv.DatesFinModification));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_SonPropreCompte_SuperAdministrateurPeutMettreUneDateDeFinPassee()
        {
            // Le super administrateur peut tout faire sur son compte, même se retirer l'accès
            var hier = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            var srv = MonCompte("S");
            var c = CreerControllerConnecte("moi@test.fr", srv, "S");

            await c.EditAccount(1, "Moi", "moi@test.fr", "S", hier);

            Assert.Equal(hier, Assert.Single(srv.DatesFinModification));
            Assert.Null(Erreur(c));
        }

        [Fact]
        public async Task EditAccount_AutreCompte_DateDeFinEstTransmise_MemePassee()
        {
            var fin = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            var srv = DeuxUtilisateurs();
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Un", "un@test.fr", "R", fin);

            Assert.Equal(fin, Assert.Single(srv.DatesFinModification));
        }

        [Fact]
        public async Task EditAccount_DateDeFinVide_EstTransmiseVide()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 1, UserEmail = "un@test.fr", Access = "R", DateFinu = new DateOnly(2030, 1, 1) } } };

            await CreerController(srv).EditAccount(1, "", "un@test.fr", "R", null);

            Assert.Null(Assert.Single(srv.DatesFinModification));
        }

        [Fact]
        public async Task AddAccount_DateDeFinEstTransmise()
        {
            var fin = new DateOnly(2030, 6, 30);
            var srv = new FauxUtilisateurSrv();

            await CreerController(srv).AddAccount("nouveau@test.fr", "R", fin);

            Assert.Equal(fin, Assert.Single(srv.DatesFinAjout));
        }

        [Fact]
        public async Task EditAccount_AutreCompte_AdministrateurPeutChangerEmailEtRole()
        {
            var srv = DeuxUtilisateurs();
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.EditAccount(1, "Un", "nouveau@test.fr", "W");

            Assert.Equal((1, "Un", "nouveau@test.fr", "W"), Assert.Single(srv.Modifications));
        }

        [Fact]
        public async Task DeleteAccount_UtilisateurAuDroitSuperieur_EstRefuse_AvecUneErreur()
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 2, UserEmail = "chef@test.fr", Access = "S" } } };
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.DeleteAccount(2);

            Assert.Empty(srv.Suppressions);
            Assert.Equal("Vous ne pouvez pas supprimer un utilisateur dont le rôle est supérieur au vôtre.", Erreur(c));
        }

        [Theory]
        [InlineData("W")]
        [InlineData("A")]
        public async Task DeleteAccount_DroitInferieurOuEgal_EstSupprime(string droitCible)
        {
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 2, UserEmail = "autre@test.fr", Access = droitCible } } };
            var c = CreerControllerConnecte("moi@test.fr", srv, "A");

            await c.DeleteAccount(2);

            Assert.Equal(new[] { 2 }, srv.Suppressions);
            Assert.Null(Erreur(c));
        }

        // ---------- Mémoire des sessions : un changement s'applique dès la page suivante ----------

        [Fact]
        public async Task EditAccount_OublieLEtatEnMemoire_DeLAncienneEtDeLaNouvelleAdresse()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("session:un@test.fr", "ancien état");
            cache.Set("session:nouveau@test.fr", "ancien état");
            var srv = DeuxUtilisateurs();

            await CreerController(srv, "S", cache).EditAccount(1, "Un", "nouveau@test.fr", "A");

            Assert.False(cache.TryGetValue("session:un@test.fr", out _));
            Assert.False(cache.TryGetValue("session:nouveau@test.fr", out _));
        }

        [Fact]
        public async Task DeleteAccount_OublieLEtatEnMemoire_PourCouperLaSessionTouteDeSuite()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("session:autre@test.fr", "ancien état");
            var srv = new FauxUtilisateurSrv { Utilisateurs = { new Utilisateur { IdUser = 2, UserEmail = "autre@test.fr", Access = "R" } } };
            var c = CreerController(srv, "S", cache);
            c.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "moi@test.fr"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "S")
            }, "test"));

            await c.DeleteAccount(2);

            Assert.False(cache.TryGetValue("session:autre@test.fr", out _));
        }

        [Fact]
        public async Task AddAccount_OublieLEtatEnMemoireDeLaNouvelleAdresse()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("session:nouveau@test.fr", "ancien état");

            await CreerController(new FauxUtilisateurSrv(), "S", cache).AddAccount("nouveau@test.fr", "R");

            Assert.False(cache.TryGetValue("session:nouveau@test.fr", out _));
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

            Assert.Equal(new[] { "email", "role", "dateFin" }, noms);
        }

        [Fact]
        public void EditAccount_RecoitLesChampsEnvoyesParLeFormulaire()
        {
            // Les champs du formulaire (Views/Account/AllAccounts.cshtml) s'appellent id, name, email et role :
            // avec d'autres noms, le formulaire arriverait vide et effacerait l'utilisateur.
            var noms = typeof(AccountController).GetMethod(nameof(AccountController.EditAccount))!.GetParameters().Select(p => p.Name);

            Assert.Equal(new[] { "id", "name", "email", "role", "dateFin" }, noms);
        }
    }
}
