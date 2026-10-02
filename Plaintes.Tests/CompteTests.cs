using Microsoft.AspNetCore.Mvc;
using Plaintes.Models;
using System.Security.Claims;

namespace Plaintes.Tests
{
    public class CompteTests
    {
        [Theory]
        [InlineData("R", "Lecture")]
        [InlineData("W", "Écriture")]
        [InlineData("A", "Administrateur")]
        [InlineData("S", "Super administrateur")]
        [InlineData("w", "Écriture")]
        [InlineData(" a ", "Administrateur")]
        [InlineData("X", "Aucun droit")]
        [InlineData("", "Aucun droit")]
        [InlineData(null, "Aucun droit")]
        public void DroitLibelle_TraduitLaLettreEnFrancais(string? lettre, string attendu)
        {
            Assert.Equal(attendu, new CompteViewModel { Droit = lettre }.DroitLibelle);
        }

        [Fact]
        public void Index_AfficheNomEmailEtDroitsDeLUtilisateurConnecte()
        {
            var controller = AccountControllerTests.CreerController();
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Dupont Jean"),
                new Claim(ClaimTypes.Email, "jean.dupont@test.fr"),
                new Claim(ClaimTypes.Role, "A"),
            }, "test"));

            var view = Assert.IsType<ViewResult>(controller.Index());

            var model = Assert.IsType<CompteViewModel>(view.Model);
            Assert.Equal("Dupont Jean", model.Nom);
            Assert.Equal("jean.dupont@test.fr", model.Email);
            Assert.Equal("Administrateur", model.DroitLibelle);
        }
    }
}
