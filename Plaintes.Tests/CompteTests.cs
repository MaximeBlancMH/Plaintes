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
    }
}
