using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Plaintes.Models;

namespace Plaintes.Tests
{
    /// <summary>
    /// composer.NUMDETAIL est une colonne IDENTITY : EF ne doit pas envoyer de valeur. Avec ValueGeneratedNever, chaque nouveau détail
    /// partait avec la clé 0 et le deuxième provoquait « Violation of PRIMARY KEY constraint 'PK_Composer' ».
    /// </summary>
    public class ComposerCleTests
    {
        [Fact]
        public void NumDetail_EstGenereParLaBase()
        {
            using var ctx = TestDb.Creer();

            var propriete = ctx.Model.FindEntityType(typeof(Composer))!.FindProperty(nameof(Composer.NumDetail))!;

            Assert.Equal(ValueGenerated.OnAdd, propriete.ValueGenerated);
        }

        [Fact]
        public async Task PlusieursDetailsAjoutesEnsemble_ObtiennentDesNumerosDifferents()
        {
            using var ctx = TestDb.Creer();
            ctx.Composers.AddRange(
                new Composer { NumPlainte = 1, CodeNuisance = "N1", Cout = 10 },
                new Composer { NumPlainte = 1, CodeNuisance = "N2", Cout = 20 },
                new Composer { NumPlainte = 1, CodeNuisance = "N3", Cout = 30 });

            await ctx.SaveChangesAsync();

            var numeros = await ctx.Composers.Select(c => c.NumDetail).ToListAsync();
            Assert.Equal(3, numeros.Distinct().Count());
            Assert.DoesNotContain(0, numeros);
        }
    }
}
