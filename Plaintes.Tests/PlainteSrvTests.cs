using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;

namespace Plaintes.Tests
{
    public class PlainteSrvTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.CreerAvecDonnees();
        private readonly PlainteSrv _srv;

        public PlainteSrvTests()
        {
            _srv = new PlainteSrv(_ctx, NullLogger<PlainteSrv>.Instance);
        }

        public void Dispose() => _ctx.Dispose();

        private static int[] Numeros(PagedResult<Plainte> r) => r.Plaintes.Select(p => p.NumPlainte).ToArray();

        // ---------- getPlaintes ----------

        [Fact]
        public async Task GetPlaintes_ExclutLesSupprimees_EtTrieParDateCreationDecroissante()
        {
            var result = await _srv.getPlaintes();

            Assert.Equal(new[] { 3, 2, 1 }, result.Select(p => p.NumPlainte));
        }

        [Fact]
        public async Task GetPlaintes_ExclutLesDetailsSupprimes()
        {
            var result = await _srv.getPlaintes();

            var plainte1 = result.Single(p => p.NumPlainte == 1);
            Assert.Single(plainte1.Composers);
            Assert.Equal(100, plainte1.Composers.Single().Cout);
        }

        // ---------- getPlaintesForPage ----------

        [Fact]
        public async Task GetPlaintesForPage_RenvoieLaPageDemandee()
        {
            var page1 = await _srv.getPlaintesForPage(1, 2);
            var page2 = await _srv.getPlaintesForPage(2, 2);

            Assert.Equal(3, page1.TotalCount);
            Assert.Equal(2, page1.TotalPages);
            Assert.Equal(new[] { 3, 2 }, Numeros(page1));
            Assert.Equal(new[] { 1 }, Numeros(page2));
            Assert.False(page1.HasPreviousPage);
            Assert.True(page1.HasNextPage);
            Assert.True(page2.HasPreviousPage);
            Assert.False(page2.HasNextPage);
        }

        // ---------- getPlaintesWithFilters : filtres ----------

        [Fact]
        public async Task Filtres_Vides_RenvoientToutesLesPlaintesNonSupprimees()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "datecreation", "desc", 1, 10);

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(new[] { 3, 2, 1 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_NumPlainte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { numPlainte = 2 }, null!, "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_NumPlainte_SupprimeeEstIntrouvable()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { numPlainte = 4 }, null!, "asc", 1, 10);

            Assert.Empty(result.Plaintes);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task Filtre_Cite_ContientLeTexte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { cite = "Pins" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1, 3 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_Rue_ContientLeTexte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { rue = "Moulin" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_PartieCommune_ContientLeTexte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { partieCommune = "Cave" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_Entree_ContientLeTexte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { entree = "E03" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 3 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_Reclamation_ContientLeTexte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { reclamation = "R1" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_Categorie_CorrespondanceExacte()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { categorie = "C2" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_SousCategorie()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { sousCategorie = "S1" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_Nuisance()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { nuisance = "N2" }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_CoutMin_BorneInclusive()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { coutMin = 100 }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1, 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_CoutMin_IgnoreLesDetailsSupprimes()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { coutMin = 400 }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 2 }, Numeros(result));
        }

        [Fact]
        public async Task Filtre_CoutMax()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { coutMax = 200 }, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1 }, Numeros(result));
        }

        [Fact]
        public async Task Filtres_Combines()
        {
            var filtres = new PlainteFiltres { cite = "Pins", categorie = "C1", coutMax = 200 };

            var result = await _srv.getPlaintesWithFilters(filtres, "numplainte", "asc", 1, 10);

            Assert.Equal(new[] { 1 }, Numeros(result));
        }

        // ---------- getPlaintesWithFilters : tri ----------

        [Theory]
        [InlineData("numplainte", "asc", new[] { 1, 2, 3 })]
        [InlineData("numplainte", "desc", new[] { 3, 2, 1 })]
        [InlineData("cite", "asc", new[] { 2, 3, 1 })]       // "Les Chênes" avant "Les Pins" (égalité départagée par NumPlainte décroissant)
        [InlineData("cite", "desc", new[] { 3, 1, 2 })]
        [InlineData("categorie", "asc", new[] { 3, 1, 2 })]  // Bruit avant Odeur
        [InlineData("entree", "desc", new[] { 3, 2, 1 })]
        [InlineData("reclamation", "asc", new[] { 1, 2, 3 })]
        [InlineData("datecreation", "asc", new[] { 1, 2, 3 })]
        [InlineData("datecreation", "desc", new[] { 3, 2, 1 })]
        public async Task Tri_ParColonne(string tri, string sens, int[] attendu)
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres(), tri, sens, 1, 10);

            Assert.Equal(attendu, Numeros(result));
        }

        [Fact]
        public async Task Tri_ParCout_IgnoreLesDetailsSupprimes()
        {
            // Plainte 3 = 0, plainte 1 = 100 (le détail supprimé à 9999 est ignoré), plainte 2 = 500
            var asc = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "cout", "asc", 1, 10);
            var desc = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "cout", "desc", 1, 10);

            Assert.Equal(new[] { 3, 1, 2 }, Numeros(asc));
            Assert.Equal(new[] { 2, 1, 3 }, Numeros(desc));
        }

        [Fact]
        public async Task Tri_InsensibleALaCasse()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "NumPlainte", "asc", 1, 10);

            Assert.Equal(new[] { 1, 2, 3 }, Numeros(result));
        }

        [Fact]
        public async Task Tri_Inconnu_RetombeSurLaDateDeCreation()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "n'importe quoi", "asc", 1, 10);

            Assert.Equal(new[] { 1, 2, 3 }, Numeros(result));
        }

        [Fact]
        public async Task Tri_Null_RetombeSurLaDateDeCreation()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres(), null!, "desc", 1, 10);

            Assert.Equal(new[] { 3, 2, 1 }, Numeros(result));
        }

        // ---------- getPlaintesWithFilters : pagination ----------

        [Fact]
        public async Task Pagination_AvecFiltresEtTri()
        {
            var page2 = await _srv.getPlaintesWithFilters(new PlainteFiltres(), "numplainte", "asc", 2, 2);

            Assert.Equal(3, page2.TotalCount);
            Assert.Equal(2, page2.Page);
            Assert.Equal(2, page2.PageSize);
            Assert.Equal(new[] { 3 }, Numeros(page2));
        }

        [Fact]
        public async Task ResultatCharge_LesRelationsNecessairesALaVue()
        {
            var result = await _srv.getPlaintesWithFilters(new PlainteFiltres { numPlainte = 2 }, "numplainte", "asc", 1, 10);

            var p = Assert.Single(result.Plaintes);
            Assert.Equal("Dupont", p.IdUserNavigation.NomUser);
            Assert.Equal("Les Chênes", p.NumCiteNavigation.LibCite);
            Assert.Equal("Rue du Moulin", p.CodeRueNavigation.LibRue);
            Assert.Equal("Cave", p.CodePartieCommuneNavigation.LibPartieCommune);
            Assert.Equal("Odeur", p.CodeCatNavigation.LibCat);
            Assert.Equal("Poubelles", p.Composers.Single().CodeNuisanceNavigation.CodeSousCatNavigation.LibSousCat);
        }
    }
}
