using Microsoft.AspNetCore.Authorization;
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
        [InlineData(0)]     // supprimé aujourd'hui (DeleteUser met la date du jour)
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
    }
}
