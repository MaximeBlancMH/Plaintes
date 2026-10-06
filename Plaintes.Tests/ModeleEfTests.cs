using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;

namespace Plaintes.Tests
{
    /// <summary>
    /// Les tests habituels utilisent la base en mémoire, qui ne vérifie pas le modèle comme SQL Server le fait au démarrage.
    /// Ici on construit le modèle avec le fournisseur SQL Server (sans se connecter) : une erreur de mapping fait échouer le test,
    /// comme elle ferait échouer le lancement de l'application.
    /// </summary>
    public class ModeleEfTests
    {
        private static AppDbContext ContexteSqlServer() => new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=(localdb)\\inexistant;Database=PLAINTES;Trusted_Connection=True")
                .Options);

        [Fact]
        public void LeModeleSqlServer_EstValide()
        {
            using var ctx = ContexteSqlServer();

            var exception = Record.Exception(() => ctx.Model);

            Assert.Null(exception);
        }

        [Fact]
        public void CiteRue_EstLaSeuleEntiteMappeeSurCiteRue()
        {
            // Deux entités sur la même table (une classe + une relation plusieurs-à-plusieurs automatique) font planter le démarrage.
            using var ctx = ContexteSqlServer();

            var entites = ctx.Model.GetEntityTypes().Where(e => e.GetTableName() == "cite_rue").ToList();

            Assert.Equal(typeof(CiteRue), Assert.Single(entites).ClrType);
        }

        [Fact]
        public void CiteRue_ALaCleComposeeCiteEtRue()
        {
            using var ctx = ContexteSqlServer();

            var cle = ctx.Model.FindEntityType(typeof(CiteRue))!.FindPrimaryKey()!;

            Assert.Equal(new[] { nameof(CiteRue.NumCite), nameof(CiteRue.CodeRue) }, cle.Properties.Select(p => p.Name));
        }
    }
}
