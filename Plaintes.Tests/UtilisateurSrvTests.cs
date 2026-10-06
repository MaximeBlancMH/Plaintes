using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;

namespace Plaintes.Tests
{
    public class UtilisateurSrvTests
    {
        private static async Task<(AppDbContext, UtilisateurSrv)> Creer(params Utilisateur[] utilisateurs)
        {
            var ctx = TestDb.Creer();
            ctx.Utilisateurs.AddRange(utilisateurs);
            await ctx.SaveChangesAsync();
            return (ctx, new UtilisateurSrv(ctx, NullLogger<UtilisateurSrv>.Instance));
        }

        // ---------- UpdateNameUser ----------

        [Fact]
        public async Task UpdateNameUser_ModifieLeNom()
        {
            using var ctx = TestDb.Creer();
            ctx.Utilisateurs.Add(new Utilisateur { IdUser = 1, NomUser = "Ancien" });
            await ctx.SaveChangesAsync();
            var srv = new UtilisateurSrv(ctx, NullLogger<UtilisateurSrv>.Instance);

            await srv.UpdateNameUser(1, "Nouveau");

            Assert.Equal("Nouveau", (await ctx.Utilisateurs.FindAsync(1))!.NomUser);
        }

        [Fact]
        public async Task UpdateNameUser_UtilisateurInconnu_NeModifieRienEtNeLevePasDException()
        {
            using var ctx = TestDb.Creer();
            ctx.Utilisateurs.Add(new Utilisateur { IdUser = 1, NomUser = "Ancien" });
            await ctx.SaveChangesAsync();
            var srv = new UtilisateurSrv(ctx, NullLogger<UtilisateurSrv>.Instance);

            await srv.UpdateNameUser(999, "Nouveau");

            Assert.Equal("Ancien", (await ctx.Utilisateurs.FindAsync(1))!.NomUser);
            Assert.Single(ctx.Utilisateurs);
        }

        // ---------- GetAllUsers / GetUserById ----------

        [Fact]
        public async Task GetAllUsers_RenvoieTousLesUtilisateurs()
        {
            var (ctx, srv) = await Creer(new Utilisateur { IdUser = 1, NomUser = "A" }, new Utilisateur { IdUser = 2, NomUser = "B" });
            using var _ = ctx;

            var users = await srv.GetAllUsers();

            Assert.Equal(new[] { 1, 2 }, users.Select(u => u.IdUser).OrderBy(i => i));
        }

        [Fact]
        public async Task GetAllUsers_BaseVide_RenvoieUneListeVide()
        {
            var (ctx, srv) = await Creer();
            using var _ = ctx;

            Assert.Empty(await srv.GetAllUsers());
        }

        [Fact]
        public async Task GetUserById_RenvoieLUtilisateurOuNull()
        {
            var (ctx, srv) = await Creer(new Utilisateur { IdUser = 1, NomUser = "A" });
            using var _ = ctx;

            Assert.Equal("A", (await srv.GetUserById(1))!.NomUser);
            Assert.Null(await srv.GetUserById(99));
        }

        // ---------- AddUser ----------

        [Fact]
        public async Task AddUser_EnregistreNomEmailEtDroit()
        {
            var (ctx, srv) = await Creer();
            using var _ = ctx;

            await srv.AddUser("Dupont", "dupont@test.fr", "W");

            var user = Assert.Single(ctx.Utilisateurs);
            Assert.Equal("Dupont", user.NomUser);
            Assert.Equal("dupont@test.fr", user.UserEmail);
            Assert.Equal("W", user.Access);
        }

        // ---------- UpdateUser ----------

        [Fact]
        public async Task UpdateUser_ModifieNomEmailEtDroit_SansToucherAuxAutres()
        {
            var (ctx, srv) = await Creer(
                new Utilisateur { IdUser = 1, NomUser = "Ancien", UserEmail = "ancien@test.fr", Access = "R" },
                new Utilisateur { IdUser = 2, NomUser = "Autre", UserEmail = "autre@test.fr", Access = "R" });
            using var _ = ctx;

            await srv.UpdateUser(1, "Nouveau", "nouveau@test.fr", "A");

            var modifie = (await ctx.Utilisateurs.FindAsync(1))!;
            Assert.Equal(("Nouveau", "nouveau@test.fr", "A"), (modifie.NomUser, modifie.UserEmail, modifie.Access));
            var autre = (await ctx.Utilisateurs.FindAsync(2))!;
            Assert.Equal(("Autre", "autre@test.fr", "R"), (autre.NomUser, autre.UserEmail, autre.Access));
        }

        [Fact]
        public async Task UpdateUser_UtilisateurInconnu_NeModifieRienEtNeLevePasDException()
        {
            var (ctx, srv) = await Creer(new Utilisateur { IdUser = 1, NomUser = "Ancien", UserEmail = "ancien@test.fr", Access = "R" });
            using var _ = ctx;

            await srv.UpdateUser(999, "Nouveau", "nouveau@test.fr", "A");

            Assert.Equal("Ancien", (await ctx.Utilisateurs.FindAsync(1))!.NomUser);
            Assert.Single(ctx.Utilisateurs);
        }

        // ---------- DeleteUser ----------

        // Suppression logique : l'utilisateur reste en base (ses plaintes gardent leur créateur), avec une date de fin à aujourd'hui.
        [Fact]
        public async Task DeleteUser_MetUneDateDeFinAujourdhui_SansSupprimerLaLigne_NiToucherAuxAutres()
        {
            var (ctx, srv) = await Creer(new Utilisateur { IdUser = 1 }, new Utilisateur { IdUser = 2 });
            using var _ = ctx;

            await srv.DeleteUser(1);

            Assert.Equal(2, ctx.Utilisateurs.Count());
            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), (await ctx.Utilisateurs.FindAsync(1))!.DateFinu);
            Assert.Null((await ctx.Utilisateurs.FindAsync(2))!.DateFinu);
        }

        [Fact]
        public async Task DeleteUser_UtilisateurInconnu_NeModifieRienEtNeLevePasDException()
        {
            var (ctx, srv) = await Creer(new Utilisateur { IdUser = 1 });
            using var _ = ctx;

            await srv.DeleteUser(999);

            Assert.Null((await ctx.Utilisateurs.FindAsync(1))!.DateFinu);
        }
    }
}
