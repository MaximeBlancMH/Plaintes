using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;
using Plaintes.Services.Stockage;

namespace Plaintes.Tests
{
    /// <summary>Modification d'une plainte : updatePlainte et les contrôles propres à la modification dans validerPlainte.</summary>
    public class PlainteSrvModificationTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly PlainteSrv _srv;
        private readonly string _racine = Path.Combine(Path.GetTempPath(), "plaintes-tests-" + Guid.NewGuid());

        public PlainteSrvModificationTests()
        {
            // InMemory ignore les transactions : on désactive l'avertissement correspondant.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _ctx = new AppDbContext(options);

            var reglages = Options.Create(new StorageSettings { Racine = _racine, MaxSize = 1024 * 1024, MaxTotalSize = 1024 * 1024, MaxPieces = 3, Extensions = new[] { ".pdf", ".png" }, FichierTemoin = ".ok" });
            Directory.CreateDirectory(_racine);
            File.WriteAllText(Path.Combine(_racine, ".ok"), "");
            _srv = new PlainteSrv(_ctx, NullLogger<PlainteSrv>.Instance, new ConfigurationBuilder().Build(),
                new FileSystemPieceStorage(reglages, new FakeEnv()), reglages);

            var c1 = new Categorie { CodeCat = "C1", LibCat = "Bruit" };
            var c2 = new Categorie { CodeCat = "C2", LibCat = "Odeur" };
            var s1 = new SousCategorie { CodeSousCat = "S1", CodeCat = "C1", LibSousCat = "Voisinage", CodeCatNavigation = c1 };
            var s2 = new SousCategorie { CodeSousCat = "S2", CodeCat = "C2", LibSousCat = "Poubelles", CodeCatNavigation = c2 };
            _ctx.AddRange(c1, c2, s1, s2,
                new Nuisance { CodeNuisance = "N1", CodeSousCat = "S1", LibNuisance = "Musique", CodeSousCatNavigation = s1 },
                new Nuisance { CodeNuisance = "N2", CodeSousCat = "S2", LibNuisance = "Déchets", CodeSousCatNavigation = s2 },
                new Cite { NumCite = 1, LibCite = "Les Pins" },
                new Cite { NumCite = 2, LibCite = "Les Chênes" },
                new Rue { CodeRue = 1, LibRue = "Rue des Lilas" },
                new Rue { CodeRue = 2, LibRue = "Rue du Moulin" },
                new PartieCommune { CodePartieCommune = "PC1", LibPartieCommune = "Hall" },
                new PartieCommune { CodePartieCommune = "PC2", LibPartieCommune = "Cave" },
                new NaturePiece { CodeNature = "PHOT", LibNature = "Photo" },
                new TypeFait { CodeFait = "AUTR", LibFait = "Autres" },
                new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" },
                new Batiment { IdBat = 2, NumCite = 2, NumBat = "B2" });
            _ctx.AddRange(new BatimentRue { IdBat = 1, CodeRue = 1, NumRue = "12" }, new BatimentRue { IdBat = 2, CodeRue = 2, NumRue = "3" });

            _ctx.Plaintes.AddRange(
                new Plainte
                {
                    NumPlainte = 1, IdUser = 7, NumReclamation = "100-1", CodEtat = "Ec", CodeFait = null, CodePartieCommune = "PC1", NumCite = 1, CodeRue = 1,
                    NumRue = "12", DateDeb = new DateOnly(2026, 1, 1), Commentaire = "Ancien", CodeCat = "C1",
                    DateCreation = new DateTime(2026, 1, 1), DateModification = new DateTime(2026, 1, 1)
                },
                new Plainte { NumPlainte = 2, NumReclamation = "200-2", CodEtat = "Ec", CodePartieCommune = "PC1", CodeCat = "C1", NumCite = 1, CodeRue = 1, Suppr = true },
                new Plainte { NumPlainte = 3, NumReclamation = "300-3", CodEtat = "Ec", CodePartieCommune = "PC1", CodeCat = "C1", NumCite = 1, CodeRue = 1 });
            _ctx.Composers.AddRange(
                new Composer { NumDetail = 1, NumPlainte = 1, CodeNuisance = "N1", Cout = 100 },
                new Composer { NumDetail = 2, NumPlainte = 1, CodeNuisance = "N1", Cout = 9999, SupprDet = true },
                new Composer { NumDetail = 3, NumPlainte = 3, CodeNuisance = "N1", Cout = 300 });
            _ctx.PieceJointes.AddRange(
                new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "a.pdf", TypePiece = "application/pdf", TaillePiece = "1 Ko" },
                new PieceJointe { NumPiece = 2, NumPlainte = 1, LibPiece = "b.pdf", TypePiece = "application/pdf", TaillePiece = "1 Ko" },
                new PieceJointe { NumPiece = 3, NumPlainte = 1, LibPiece = "c.pdf", TypePiece = "application/pdf", TaillePiece = "1 Ko", SupprPj = true },
                new PieceJointe { NumPiece = 4, NumPlainte = 3, LibPiece = "autre-plainte.pdf", TypePiece = "application/pdf", TaillePiece = "1 Ko" });
            _ctx.SaveChanges();
            _ctx.ChangeTracker.Clear();
        }

        public void Dispose()
        {
            _ctx.Dispose();
            if (Directory.Exists(_racine)) Directory.Delete(_racine, true);
        }

        private static CreatePlainteViewModel Formulaire(Action<CreatePlainteViewModel>? modifier = null)
        {
            var model = new CreatePlainteViewModel
            {
                NumPlainte = 1,
                NumReclamation = "111-222",
                CodePartieCommune = "PC2",
                NumCite = 2,
                CodeRue = 2,
                NumRue = "3",
                DateDeb = new DateOnly(2026, 2, 1),
                DateFin = new DateOnly(2026, 2, 5),
                Commentaire = "Nouveau",
                CodeCat = "C2",
                Details = { new DetailPlainteViewModel { CodeSousCat = "S2", CodeNuisance = "N2", Cout = 42 } }
            };
            modifier?.Invoke(model);
            return model;
        }

        private static PieceJointeViewModel Piece(string nom, string contenu = "x")
        {
            var octets = System.Text.Encoding.UTF8.GetBytes(contenu);
            var fichier = new FormFile(new MemoryStream(octets), 0, octets.Length, "Fichier", nom) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
            return new PieceJointeViewModel { Fichier = fichier, CodeNature = "PHOT" };
        }

        // ---------- updatePlainte ----------

        [Fact]
        public async Task Update_ModifieLesChampsDeLaPlainte()
        {
            Assert.True(await _srv.updatePlainte(Formulaire()));

            var p = await _ctx.Plaintes.AsNoTracking().SingleAsync(x => x.NumPlainte == 1);
            Assert.Equal("111-222", p.NumReclamation);
            Assert.Equal("PC2", p.CodePartieCommune);
            Assert.Equal(2, p.NumCite);
            Assert.Equal(2, p.CodeRue);
            Assert.Equal("3", p.NumRue);
            Assert.Equal(new DateOnly(2026, 2, 1), p.DateDeb);
            Assert.Equal(new DateOnly(2026, 2, 5), p.DateFin);
            Assert.Equal("Nouveau", p.Commentaire);
            Assert.Equal("C2", p.CodeCat);
        }

        [Fact]
        public async Task Update_ConserveCreateurEtDateDeCreation_EtMetAJourLaDateDeModification()
        {
            var avant = DateTime.Now;

            await _srv.updatePlainte(Formulaire());

            var p = await _ctx.Plaintes.AsNoTracking().SingleAsync(x => x.NumPlainte == 1);
            Assert.Equal(7, p.IdUser);
            Assert.Equal("Ec", p.CodEtat);
            Assert.Equal(new DateTime(2026, 1, 1), p.DateCreation);
            Assert.True(p.DateModification >= avant);
        }

        [Fact]
        public async Task Update_PrecisionDuTypeDeFaits_EstEnregistree_QuandLeTypeEstAutres()
        {
            Assert.True(await _srv.updatePlainte(Formulaire(m => { m.CodeFait = "AUTR"; m.PrecisionFait = "  Détail  "; })));

            var p = await _ctx.Plaintes.AsNoTracking().SingleAsync(x => x.NumPlainte == 1);
            Assert.Equal("AUTR", p.CodeFait);
            Assert.Equal("Détail", p.PrecisionFait);
        }

        [Fact]
        public async Task Update_PrecisionDuTypeDeFaits_EstIgnoree_QuandLeTypeNEstPasAutres()
        {
            Assert.True(await _srv.updatePlainte(Formulaire(m => { m.CodeFait = null; m.PrecisionFait = "ignorée"; })));

            Assert.Null((await _ctx.Plaintes.AsNoTracking().SingleAsync(x => x.NumPlainte == 1)).PrecisionFait);
        }

        [Fact]
        public async Task Update_RemplaceLesDetails_EnMarquantLesAnciensCommeSupprimes_SansToucherAuxAutresPlaintes()
        {
            await _srv.updatePlainte(Formulaire());

            var details = await _ctx.Composers.AsNoTracking().Where(c => c.NumPlainte == 1).ToListAsync();
            Assert.All(details.Where(c => c.NumDetail is 1 or 2), c => Assert.True(c.SupprDet));
            var nouveau = Assert.Single(details, c => !c.SupprDet);
            Assert.Equal("N2", nouveau.CodeNuisance);
            Assert.Equal(42, nouveau.Cout);
            Assert.False((await _ctx.Composers.AsNoTracking().SingleAsync(c => c.NumDetail == 3)).SupprDet);
        }

        [Fact]
        public async Task Update_RetirePiecesDemandees_SeulementCellesDeLaPlainte()
        {
            // La pièce 4 appartient à la plainte 3 : même si son numéro est envoyé, elle n'est pas touchée.
            await _srv.updatePlainte(Formulaire(m => m.PiecesASupprimer.AddRange(new[] { 1, 4 })));

            var pieces = await _ctx.PieceJointes.AsNoTracking().ToDictionaryAsync(p => p.NumPiece);
            Assert.True(pieces[1].SupprPj);
            Assert.False(pieces[2].SupprPj);
            Assert.False(pieces[4].SupprPj);
        }

        [Fact]
        public async Task Update_AjouteLesNouvellesPieces_SurLeDisque_EtConserveLesAnciennes()
        {
            Assert.True(await _srv.updatePlainte(Formulaire(m => m.Pieces.Add(Piece("nouvelle.pdf", "contenu")))));

            var pieces = await _ctx.PieceJointes.AsNoTracking().Where(p => p.NumPlainte == 1 && !p.SupprPj).ToListAsync();
            Assert.Equal(3, pieces.Count);                                    // a.pdf, b.pdf et la nouvelle
            var nouvelle = Assert.Single(pieces, p => p.LibPiece == "nouvelle.pdf");
            Assert.StartsWith("1/", nouvelle.CheminPiece);
            Assert.True(File.Exists(Path.Combine(_racine, nouvelle.CheminPiece!)));
        }

        [Fact]
        public async Task Update_PlainteInexistanteOuSupprimee_RenvoieFalse()
        {
            Assert.False(await _srv.updatePlainte(Formulaire(m => m.NumPlainte = 99)));
            Assert.False(await _srv.updatePlainte(Formulaire(m => m.NumPlainte = 2)));
            Assert.True((await _ctx.Plaintes.AsNoTracking().SingleAsync(x => x.NumPlainte == 2)).Suppr);   // inchangée
        }

        [Fact]
        public async Task Update_EchecSurUnePiece_RenvoieFalse_EtNeLaisseAucunFichierOrphelin()
        {
            // Une pièce sans fichier fait échouer l'ajout des pièces.
            var sansFichier = new PieceJointeViewModel { CodeNature = "PHOT" };

            Assert.False(await _srv.updatePlainte(Formulaire(m => { m.Pieces.Add(Piece("ok.pdf")); m.Pieces.Add(sansFichier); })));

            Assert.Empty(Directory.GetFiles(_racine, "*.pdf", SearchOption.AllDirectories));
        }

        // ---------- validerPlainte en modification ----------

        private async Task<string?> Valider(Action<CreatePlainteViewModel>? modifier = null)
        {
            _ctx.ChangeTracker.Clear();
            return await _srv.validerPlainte(Formulaire(modifier));
        }

        [Fact]
        public async Task Valider_FormulaireDeModificationValide_Accepte() =>
            Assert.Null(await Valider());

        [Theory]
        [InlineData(99)]
        [InlineData(2)]
        public async Task Valider_PlainteInexistanteOuSupprimee_Refuse(int numPlainte) =>
            Assert.Equal("Ce dossier n'existe plus.", await Valider(m => m.NumPlainte = numPlainte));

        [Fact]
        public async Task Valider_LesPiecesDejaEnregistrees_CompteDansLaLimite()
        {
            // 2 pièces actives (la 3e est supprimée) + 2 nouvelles = 4 > 3.
            var erreur = await Valider(m => { m.Pieces.Add(Piece("n1.pdf")); m.Pieces.Add(Piece("n2.pdf")); });
            Assert.Contains("plus de 3 pièces", erreur);

            // Si l'une des anciennes est retirée : 1 + 2 = 3, accepté.
            Assert.Null(await Valider(m => { m.PiecesASupprimer.Add(1); m.Pieces.Add(Piece("n1.pdf")); m.Pieces.Add(Piece("n2.pdf")); }));
        }

        [Fact]
        public async Task Valider_Creation_NeTientPasCompteDesPiecesDUneAutrePlainte()
        {
            // NumPlainte = 0 : création ; les 2 pièces de la plainte 1 ne comptent pas.
            Assert.Null(await Valider(m => { m.NumPlainte = 0; m.Pieces.Add(Piece("n1.pdf")); m.Pieces.Add(Piece("n2.pdf")); m.Pieces.Add(Piece("n3.pdf")); }));
        }
    }
}
