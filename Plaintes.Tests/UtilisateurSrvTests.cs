using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Models;
using Plaintes.Services;

namespace Plaintes.Tests
{
    public class UtilisateurSrvTests
    {
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
    }
}
