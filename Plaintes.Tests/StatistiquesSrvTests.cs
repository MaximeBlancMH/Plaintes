using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;
using Xunit;

namespace Plaintes.Tests
{
    public class StatistiquesSrvTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();

        public void Dispose() => _ctx.Dispose();

        /// <summary>
        /// Jeu de données :
        /// - Dossier 1 : 2025, cité Pins (CENTRE), catégorie Bruit, en cours, détail Musique à 100
        /// - Dossier 2 : 2026, cité Pins, catégorie Bruit, détail Musique à 200 (+ un détail supprimé à 9999)
        /// - Dossier 3 : 2026, cité Chênes (SUD), catégorie Odeur, détail Déchets sans coût
        /// - Dossier 4 : 2026, supprimé
        /// </summary>
        private async Task Preparer()
        {
            _ctx.AddRange(
                new Cite { NumCite = 1, LibCite = "Les Pins", Secteur = "CENTRE" },
                new Cite { NumCite = 2, LibCite = "Les Chênes", Secteur = "SUD" },
                new Categorie { CodeCat = "C1", LibCat = "Bruit" },
                new Categorie { CodeCat = "C2", LibCat = "Odeur" },
                new Nuisance { CodeNuisance = "N1", LibNuisance = "Musique", CodeSousCat = "S1" },
                new Nuisance { CodeNuisance = "N2", LibNuisance = "Déchets", CodeSousCat = "S2" });

            Plainte P(int num, int cite, string cat, int annee, int mois, string etat = "Ce", bool suppr = false) => new()
            {
                NumPlainte = num, NumCite = cite, CodeCat = cat, CodEtat = etat, Suppr = suppr, NumReclamation = num.ToString(),
                CodePartieCommune = "PC1", DateDeb = new DateOnly(annee, mois, 1)
            };
            _ctx.AddRange(P(1, 1, "C1", 2025, 12, "Ec"), P(2, 1, "C1", 2026, 1), P(3, 2, "C2", 2026, 1), P(4, 1, "C1", 2026, 2, suppr: true));
            _ctx.AddRange(
                new Composer { NumDetail = 1, NumPlainte = 1, CodeNuisance = "N1", Cout = 100 },
                new Composer { NumDetail = 2, NumPlainte = 2, CodeNuisance = "N1", Cout = 200 },
                new Composer { NumDetail = 3, NumPlainte = 2, CodeNuisance = "N1", Cout = 9999, SupprDet = true },
                new Composer { NumDetail = 4, NumPlainte = 3, CodeNuisance = "N2" },
                new Composer { NumDetail = 5, NumPlainte = 4, CodeNuisance = "N1", Cout = 5000 });
            await _ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task ToutesLesAnnees_IgnoreLesDossiersEtDetailsSupprimes()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(3, vm.Total);
            Assert.Equal(1, vm.EnCours);
            Assert.Equal(300, vm.CoutTotal);
            Assert.Equal(100, vm.CoutMoyen);
        }

        [Fact]
        public async Task Annees_SontTrieesDuPlusRecentAuPlusAncien()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(new[] { 2026, 2025 }, vm.Annees);
        }

        [Fact]
        public async Task AnneeChoisie_NeGardeQueCetteAnnee()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(2026);

            Assert.Equal(2, vm.Total);
            Assert.Equal(0, vm.EnCours);
            Assert.Equal(200, vm.CoutTotal);
        }

        [Fact]
        public async Task EvolutionDUneAnnee_DonneLes12Mois()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(2026);

            Assert.Equal(12, vm.Evolution.Count);
            Assert.Equal(2, vm.Evolution[0].Valeur);
            Assert.Equal(0, vm.Evolution[1].Valeur);
        }

        [Fact]
        public async Task EvolutionToutesAnnees_DonneUneLigneParAnnee()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(new[] { ("2025", 1), ("2026", 2) }, vm.Evolution.Select(l => (l.Libelle, l.Valeur)));
        }

        [Fact]
        public async Task Classements_SontTriesParNombreDeDossiers()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(new[] { ("Les Pins", 2), ("Les Chênes", 1) }, vm.Cites.Select(l => (l.Libelle, l.Valeur)));
            Assert.Equal(new[] { ("CENTRE", 2), ("SUD", 1) }, vm.Secteurs.Select(l => (l.Libelle, l.Valeur)));
            Assert.Equal(new[] { ("Bruit", 2), ("Odeur", 1) }, vm.Categories.Select(l => (l.Libelle, l.Valeur)));
            Assert.Equal(new[] { ("Musique", 2), ("Déchets", 1) }, vm.Nuisances.Select(l => (l.Libelle, l.Valeur)));
        }

        [Fact]
        public async Task EvolutionPrecedente_EstCelleDeLAnneeDAvant()
        {
            await Preparer();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(2026);

            Assert.Equal(12, vm.EvolutionPrecedente.Count);
            Assert.Equal(1, vm.EvolutionPrecedente[11].Valeur);
            Assert.Empty((await new StatistiquesSrv(_ctx).GetStatistiques(null)).EvolutionPrecedente);
        }

        [Fact]
        public async Task Categories_AuDela6SontRegroupeesDansAutres()
        {
            for (int i = 1; i <= 8; i++)
            {
                _ctx.Add(new Categorie { CodeCat = "C" + i, LibCat = "Cat " + i });
                _ctx.Add(new Plainte { NumPlainte = i, NumCite = 1, CodeCat = "C" + i, CodEtat = "Ec", NumReclamation = "1", CodePartieCommune = "A", DateDeb = new DateOnly(2026, 1, 1) });
            }
            await _ctx.SaveChangesAsync();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(7, vm.Categories.Count);
            Assert.Equal(("Autres", 2), (vm.Categories[6].Libelle, vm.Categories[6].Valeur));
            Assert.Equal(8, vm.Categories.Sum(l => l.Valeur));
        }

        [Fact]
        public async Task SecteurVide_EstRegroupeSousNonRenseigne()
        {
            _ctx.AddRange(new Cite { NumCite = 1, LibCite = "X" },
                new Plainte { NumPlainte = 1, NumCite = 1, CodeCat = "C", CodEtat = "Ec", NumReclamation = "1", CodePartieCommune = "A", DateDeb = new DateOnly(2026, 1, 1) });
            await _ctx.SaveChangesAsync();

            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal("Non renseigné", Assert.Single(vm.Secteurs).Libelle);
        }

        [Fact]
        public async Task SansDossier_TousLesCompteursSontAZero()
        {
            var vm = await new StatistiquesSrv(_ctx).GetStatistiques(null);

            Assert.Equal(0, vm.Total);
            Assert.Equal(0, vm.CoutMoyen);
            Assert.Empty(vm.Evolution);
            Assert.Empty(vm.Cites);
        }
    }
}
