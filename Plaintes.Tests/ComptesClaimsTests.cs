using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Plaintes.Models;
using Plaintes.Services;
using System.Security.Claims;

namespace Plaintes.Tests
{
    /// <summary>Règles de connexion : compte inconnu ou supprimé (DateFinu renseignée) = compte refusé.</summary>
    public class ComptesClaimsTests
    {
        // ---------- EstActif ----------

        [Fact]
        public void EstActif_UtilisateurSansDateDeFin_EstActif() =>
            Assert.True(ComptesClaims.EstActif(new Utilisateur { IdUser = 1 }));

        [Fact]
        public void EstActif_UtilisateurInconnu_NEstPasActif() =>
            Assert.False(ComptesClaims.EstActif(null));

        [Theory]
        [InlineData(-30)]   // date de fin passée
        [InlineData(0)]     // date du jour
        [InlineData(30)]    // même une date future : dès qu'elle est renseignée, l'utilisateur est supprimé
        public void EstActif_DateDeFinRenseignee_NEstPasActif(int jours)
        {
            var user = new Utilisateur { IdUser = 1, DateFinu = DateOnly.FromDateTime(DateTime.Today.AddDays(jours)) };

            Assert.False(ComptesClaims.EstActif(user));
        }

        // ---------- Politique par défaut ----------

        private static ClaimsPrincipal Connecte(bool refuse = false, string? role = null)
        {
            var claims = new List<Claim>();
            if (refuse) claims.Add(new Claim(ComptesClaims.Refuse, "1"));
            if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private static async Task<bool> Autorise(ClaimsPrincipal utilisateur, AuthorizationPolicy politique)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization();
            using var provider = services.BuildServiceProvider();
            var resultat = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(utilisateur, null, politique);
            return resultat.Succeeded;
        }

        [Fact]
        public async Task Politique_UtilisateurConnuEtConnecte_EstAutorise() =>
            Assert.True(await Autorise(Connecte(), ComptesClaims.PolitiqueParDefaut()));

        [Fact]
        public async Task Politique_CompteRefuse_EstRefuse_MemeConnecte() =>
            Assert.False(await Autorise(Connecte(refuse: true), ComptesClaims.PolitiqueParDefaut()));

        [Fact]
        public async Task Politique_NonConnecte_EstRefuse() =>
            Assert.False(await Autorise(new ClaimsPrincipal(new ClaimsIdentity()), ComptesClaims.PolitiqueParDefaut()));

        [Fact]
        public async Task ActionReserveeAuxAdministrateurs_RefuseUnCompteRefuse_EtLesDroitsInsuffisants()
        {
            // Avec Roles, ASP.NET n'applique pas la politique par défaut : seul le droit compte. Un compte refusé ne reçoit jamais de droit
            // (voir Program.cs), donc il est bloqué ici aussi.
            var attribut = new AuthorizeAttribute { Roles = "A,S" };
            var combinee = await AuthorizationPolicy.CombineAsync(new DefaultPolicyProvider(), new[] { attribut });

            Assert.False(await Autorise(Connecte(refuse: true), combinee!));
            Assert.True(await Autorise(Connecte(role: "A"), combinee!));
            Assert.False(await Autorise(Connecte(role: "R"), combinee!));
        }

        private sealed class DefaultPolicyProvider : IAuthorizationPolicyProvider
        {
            public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(ComptesClaims.PolitiqueParDefaut());
            public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);
            public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(null);
        }

        // ---------- EstRefuse ----------

        [Fact]
        public void EstRefuse_SeulLeClaimDeRefusEstPrisEnCompte()
        {
            Assert.True(ComptesClaims.EstRefuse(Connecte(refuse: true)));
            Assert.False(ComptesClaims.EstRefuse(Connecte()));
            Assert.False(ComptesClaims.EstRefuse(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        // ---------- SessionEstValideAsync (suppression de la session d'un utilisateur supprimé) ----------

        private static ClaimsPrincipal Session(string? email) =>
            new(new ClaimsIdentity(email == null ? new List<Claim>() : new List<Claim> { new(ClaimTypes.Email, email) }, "test"));

        private static MemoryCache NouveauCache() => new(new MemoryCacheOptions());

        [Fact]
        public async Task Session_UtilisateurActif_EstValide()
        {
            using var db = TestDb.Creer();
            db.Utilisateurs.Add(new Utilisateur { IdUser = 1, UserEmail = "a@test.fr" });
            await db.SaveChangesAsync();

            Assert.True(await ComptesClaims.SessionEstValideAsync(Session("a@test.fr"), db, NouveauCache()));
        }

        [Fact]
        public async Task Session_UtilisateurSupprime_NEstPlusValide()
        {
            using var db = TestDb.Creer();
            db.Utilisateurs.Add(new Utilisateur { IdUser = 1, UserEmail = "a@test.fr", DateFinu = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) });
            await db.SaveChangesAsync();

            Assert.False(await ComptesClaims.SessionEstValideAsync(Session("a@test.fr"), db, NouveauCache()));
        }

        [Fact]
        public async Task Session_UtilisateurInconnuOuSansAdresse_NEstPasValide()
        {
            using var db = TestDb.Creer();

            Assert.False(await ComptesClaims.SessionEstValideAsync(Session("inconnu@test.fr"), db, NouveauCache()));
            Assert.False(await ComptesClaims.SessionEstValideAsync(Session(null), db, NouveauCache()));
        }

        [Fact]
        public async Task Session_CompteDejaRefuse_NEstPasVerifie()
        {
            using var db = TestDb.Creer();
            var refuse = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ComptesClaims.Refuse, "1") }, "test"));

            Assert.True(await ComptesClaims.SessionEstValideAsync(refuse, db, NouveauCache()));
        }

        [Fact]
        public async Task Session_LeResultatEstGardeEnMemoire_PuisLaSuppressionEstPriseEnCompte()
        {
            using var db = TestDb.Creer();
            db.Utilisateurs.Add(new Utilisateur { IdUser = 1, UserEmail = "a@test.fr" });
            await db.SaveChangesAsync();
            var cache = NouveauCache();
            Assert.True(await ComptesClaims.SessionEstValideAsync(Session("a@test.fr"), db, cache));

            // L'utilisateur est supprimé : tant que le résultat est en mémoire, la session reste valable...
            (await db.Utilisateurs.FindAsync(1))!.DateFinu = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            await db.SaveChangesAsync();
            Assert.True(await ComptesClaims.SessionEstValideAsync(Session("a@test.fr"), db, cache));

            // ... puis elle est rejetée dès que la mémoire est vidée (au bout de DureeVerificationSession)
            cache.Clear();
            Assert.False(await ComptesClaims.SessionEstValideAsync(Session("a@test.fr"), db, cache));
        }

        // ---------- EtatSessionAsync (droit mis à jour sans reconnexion) ----------

        [Fact]
        public async Task EtatSession_RenvoieLeDroitActuelDeLaBase()
        {
            using var db = TestDb.Creer();
            db.Utilisateurs.Add(new Utilisateur { IdUser = 1, UserEmail = "a@test.fr", Access = "W" });
            await db.SaveChangesAsync();

            var etat = await ComptesClaims.EtatSessionAsync(Session("a@test.fr"), db, NouveauCache());

            Assert.True(etat.Valide);
            Assert.Equal("W", etat.Droit);
        }

        [Fact]
        public async Task EtatSession_UnDroitModifieEnBaseEstPrisEnCompteApresLeDelai()
        {
            using var db = TestDb.Creer();
            db.Utilisateurs.Add(new Utilisateur { IdUser = 1, UserEmail = "a@test.fr", Access = "R" });
            await db.SaveChangesAsync();
            var cache = NouveauCache();
            Assert.Equal("R", (await ComptesClaims.EtatSessionAsync(Session("a@test.fr"), db, cache)).Droit);

            (await db.Utilisateurs.FindAsync(1))!.Access = "A";
            await db.SaveChangesAsync();
            Assert.Equal("R", (await ComptesClaims.EtatSessionAsync(Session("a@test.fr"), db, cache)).Droit);   // encore en mémoire

            cache.Clear();                                                                                        // délai écoulé
            Assert.Equal("A", (await ComptesClaims.EtatSessionAsync(Session("a@test.fr"), db, cache)).Droit);
        }

        [Fact]
        public async Task EtatSession_CompteRefuse_NAPasDeDroitAMettreAJour()
        {
            using var db = TestDb.Creer();
            var refuse = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ComptesClaims.Refuse, "1") }, "test"));

            var etat = await ComptesClaims.EtatSessionAsync(refuse, db, NouveauCache());

            Assert.True(etat.Valide);
            Assert.Null(etat.Droit);
        }
    }
}
