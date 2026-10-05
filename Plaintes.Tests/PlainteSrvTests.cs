using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;
using Plaintes.Services.Stockage;
using Microsoft.Extensions.Options;

namespace Plaintes.Tests
{
    public class PlainteSrvTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.CreerAvecDonnees();
        private readonly PlainteSrv _srv;
        private readonly string _racine = Path.Combine(Path.GetTempPath(), "plaintes-tests-" + Guid.NewGuid());

        public PlainteSrvTests()
        {
            var reglages = Options.Create(new StorageSettings { Racine = _racine, MaxSize = 1024 * 1024, Extensions = new[] { ".pdf", ".png" } });
            _srv = new PlainteSrv(_ctx, NullLogger<PlainteSrv>.Instance, new ConfigurationBuilder().Build(),
                new FileSystemPieceStorage(reglages, new FakeEnv()));
        }

        public void Dispose()
        {
            _ctx.Dispose();
            if (Directory.Exists(_racine)) Directory.Delete(_racine, true);
        }

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

        // ---------- validerPlainte ----------

        private static CreatePlainteViewModel FormulaireValide() => new()
        {
            NumReclamation = "123-456",
            CodePartieCommune = "PC1",
            NumCite = 1,
            CodeRue = 1,
            NumRue = "12",
            DateDeb = new DateOnly(2026, 1, 1),
            CodeCat = "C1",
            Details = { new DetailPlainteViewModel { CodeSousCat = "S1", CodeNuisance = "N1", Cout = 50 } }
        };

        private async Task<string?> Valider(Action<CreatePlainteViewModel>? modifier = null)
        {
            // L'adresse 12 de la rue 1 appartient à la cité 1.
            if (!_ctx.Batiments.Any())
            {
                _ctx.Add(new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" });
                _ctx.Add(new BatimentRue { IdBat = 1, CodeRue = 1, NumRue = "12" });
            }
            await _ctx.SaveChangesAsync();
            var model = FormulaireValide();
            modifier?.Invoke(model);
            return await _srv.validerPlainte(model);
        }

        [Fact]
        public async Task Valider_FormulaireConforme_RenvoieNull() => Assert.Null(await Valider());

        [Fact]
        public async Task Valider_ReclamationTropLongue_RenvoieErreur() =>
            Assert.Contains("12 caractères", await Valider(m => m.NumReclamation = "1234567-1234567"));

        [Fact]
        public async Task Valider_ReclamationVide_RenvoieErreur() =>
            Assert.Contains("réclamation", await Valider(m => m.NumReclamation = " "));

        [Fact]
        public async Task Valider_PartieCommuneInconnue_RenvoieErreur() =>
            Assert.Contains("partie commune", await Valider(m => m.CodePartieCommune = "ZZ"));

        [Fact]
        public async Task Valider_AdresseHorsCite_RenvoieErreur() =>
            Assert.Contains("n'appartient pas", await Valider(m => m.NumCite = 2));

        [Fact]
        public async Task Valider_DateFinAvantDebut_RenvoieErreur() =>
            Assert.Contains("date de fin", await Valider(m => m.DateFin = new DateOnly(2025, 12, 31)));

        [Fact]
        public async Task Valider_CommentaireTropLong_RenvoieErreur() =>
            Assert.Contains("250", await Valider(m => m.Commentaire = new string('x', 251)));

        [Fact]
        public async Task Valider_NuisanceDUneAutreCategorie_RenvoieErreur() =>
            Assert.Contains("catégorie", await Valider(m => m.Details[0].CodeNuisance = "N2"));

        [Fact]
        public async Task Valider_TypeFaitAutres_SansPrecision_RenvoieErreur()
        {
            _ctx.Add(new TypeFait { CodeFait = "AUTR", LibFait = "Autres" });
            await _ctx.SaveChangesAsync();

            Assert.Contains("Précisez le type de faits", await Valider(m => m.CodeFait = "AUTR"));
            Assert.Null(await Valider(m => { m.CodeFait = "AUTR"; m.PrecisionFait = "Autre fait"; }));
        }

        [Fact]
        public async Task Valider_NatureAutre_SansPrecision_RenvoieErreur()
        {
            _ctx.Add(new NaturePiece { CodeNature = "AUTR", LibNature = "Autre" });
            await _ctx.SaveChangesAsync();
            var fichier = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "f", "a.pdf") { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = "application/pdf" };

            Assert.Contains("précisez la nature", await Valider(m => m.Pieces.Add(new PieceJointeViewModel { Fichier = fichier, CodeNature = "AUTR" })));
            Assert.Null(await Valider(m => m.Pieces.Add(new PieceJointeViewModel { Fichier = fichier, CodeNature = "AUTR", PrecisionNature = "Courrier" })));
        }

        // ---------- AddPiecesAsync ----------

        private static PieceJointeViewModel Piece(string nom, int octets = 10, string nature = "PHOT") => new()
        {
            Fichier = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[octets]), 0, octets, "f", nom)
            { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = "application/pdf" },
            CodeNature = nature
        };

        [Fact]
        public async Task AddPieces_EcritLeFichier_EtEnregistreLeCheminEtLeHash()
        {
            await _srv.AddPiecesAsync(7, new[] { Piece(@"C:\Users\x\facture.PDF", 2048) });

            var pj = Assert.Single(_ctx.PieceJointes);
            Assert.Equal("facture.PDF", pj.LibPiece);            // le dossier d'origine est retiré
            Assert.Equal("2 Ko", pj.TaillePiece);
            Assert.Null(pj.Fichier);                             // plus de contenu en base
            Assert.Matches(@"^7/[0-9a-f-]{36}\.pdf$", pj.CheminPiece);   // toujours des "/"
            Assert.Equal(64, pj.HashPiece!.Length);
            Assert.True(File.Exists(Path.Combine(_racine, pj.CheminPiece!)));
        }

        [Fact]
        public async Task AddPieces_UneExtensionRefusee_NEnregistreRienEtNettoieLeDisque()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _srv.AddPiecesAsync(7, new[] { Piece("ok.pdf"), Piece("virus.exe") }));

            Assert.Empty(_ctx.PieceJointes);
            Assert.Empty(Directory.GetFiles(_racine, "*", SearchOption.AllDirectories));
        }

        // ---------- getPlaintesWithFilters : tri ----------

        [Theory]
        [InlineData("numplainte", "asc", new[] { 1, 2, 3 })]
        [InlineData("numplainte", "desc", new[] { 3, 2, 1 })]
        [InlineData("cite", "asc", new[] { 2, 3, 1 })]       // "Les Chênes" avant "Les Pins" (égalité départagée par NumPlainte décroissant)
        [InlineData("cite", "desc", new[] { 3, 1, 2 })]
        [InlineData("categorie", "asc", new[] { 3, 1, 2 })]  // Bruit avant Odeur
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
