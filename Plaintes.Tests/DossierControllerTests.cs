using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Controllers;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using Plaintes.Services.Stockage;
using Microsoft.Extensions.Options;

namespace Plaintes.Tests
{
    public class DossierControllerTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();
        private readonly FakePlainteSrv _srv = new();
        private readonly DossierController _controller;
        private readonly string _racine = Path.Combine(Path.GetTempPath(), "plaintes-tests-" + Guid.NewGuid());
        private readonly FileSystemPieceStorage _storage;

        public DossierControllerTests()
        {
            _storage = new FileSystemPieceStorage(
                Options.Create(new StorageSettings { Racine = _racine, Extensions = new[] { ".pdf", ".png", ".docx" } }), new FakeEnv());
            _controller = new DossierController(NullLogger<DossierController>.Instance, _ctx, _srv, _storage)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void Dispose()
        {
            _ctx.Dispose();
            if (Directory.Exists(_racine)) Directory.Delete(_racine, true);
        }

        /// <summary>Faux service : mémorise les paramètres reçus et renvoie un résultat prédéfini.</summary>
        private sealed class FakePlainteSrv : IPlainteSrv
        {
            public PagedResult<Plainte> Resultat { get; set; } = new();
            public PlainteFiltres? Filtres { get; private set; }
            public string? Tri { get; private set; }
            public string? Sens { get; private set; }
            public int Page { get; private set; }
            public int PageSize { get; private set; }



            public Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize)
            {
                Filtres = filtres;
                Tri = tri;
                Sens = sens;
                Page = pageNumber;
                PageSize = pageSize;
                return Task.FromResult(Resultat);
            }

            public Task<byte[]> createPlainteFile(Dictionary<string, string> valeurs) => Task.FromResult(new byte[0]);
            
            public Task<Dictionary<string, string>> getValuesForPlainteFile(Plainte plainte) => Task.FromResult(new Dictionary<string, string>());
        
            public Task<bool> createPlainte(CreatePlainteViewModel model, string userEmail) => Task.FromResult(true);
            public CreatePlainteViewModel? Modifie { get; private set; }
            public bool ResultatModification { get; set; } = true;
            public string? ErreurValidation { get; set; }
            public Task<bool> updatePlainte(CreatePlainteViewModel model) { Modifie = model; return Task.FromResult(ResultatModification); }
            public Task<string?> validerPlainte(CreatePlainteViewModel model) => Task.FromResult(ErreurValidation);
            public Task AddPiecesAsync(int numPlainte, IEnumerable<PieceJointeViewModel> pieces, CancellationToken ct = default) => Task.CompletedTask;
        }

        // ---------- Dossiers ----------

        [Theory]
        [InlineData(10, 10)]
        [InlineData(25, 25)]
        [InlineData(50, 50)]
        [InlineData(7, 10)]
        [InlineData(0, 10)]
        [InlineData(1000, 10)]
        public async Task Dossiers_NAccepteQueLesTaillesDePageAutorisees(int demande, int attendu)
        {
            await _controller.Dossiers(new PlainteFiltres(), "cite", "asc", 3, demande);

            Assert.Equal(attendu, _srv.PageSize);
            Assert.Equal(3, _srv.Page);
        }

        [Fact]
        public async Task Dossiers_TransmetFiltresEtTriAuService()
        {
            var filtres = new PlainteFiltres { cite = "Pins" };

            await _controller.Dossiers(filtres, "rue", "desc");

            Assert.Same(filtres, _srv.Filtres);
            Assert.Equal("rue", _srv.Tri);
            Assert.Equal("desc", _srv.Sens);
            Assert.Equal(1, _srv.Page);
            Assert.Equal(10, _srv.PageSize);
        }

        [Fact]
        public async Task Dossiers_AssocieLesPiecesJointesNonSupprimees_SansLeurFichier()
        {
            _ctx.PieceJointes.AddRange(
                new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "a.pdf", TypePiece = "application/pdf", TaillePiece = "10", Fichier = new byte[] { 1, 2, 3 } },
                new PieceJointe { NumPiece = 2, NumPlainte = 1, LibPiece = "supprimee.pdf", TypePiece = "application/pdf", TaillePiece = "10", SupprPj = true },
                new PieceJointe { NumPiece = 3, NumPlainte = 2, LibPiece = "b.png", TypePiece = "image/png", TaillePiece = "20", Fichier = new byte[] { 4 } },
                new PieceJointe { NumPiece = 4, NumPlainte = 99, LibPiece = "autre.png", TypePiece = "image/png", TaillePiece = "20" });
            await _ctx.SaveChangesAsync();
            _srv.Resultat = new PagedResult<Plainte>
            {
                Plaintes = { new Plainte { NumPlainte = 1 }, new Plainte { NumPlainte = 2 }, new Plainte { NumPlainte = 3 } }
            };

            var view = Assert.IsType<ViewResult>(await _controller.Dossiers(new PlainteFiltres(), "numplainte", "asc"));

            var model = Assert.IsType<PagedResult<Plainte>>(view.Model);
            var p1 = model.Plaintes.Single(p => p.NumPlainte == 1);
            var p2 = model.Plaintes.Single(p => p.NumPlainte == 2);
            var p3 = model.Plaintes.Single(p => p.NumPlainte == 3);
            Assert.Equal(new[] { 1 }, p1.PieceJointes.Select(pj => pj.NumPiece));
            Assert.Equal(new[] { 3 }, p2.PieceJointes.Select(pj => pj.NumPiece));
            Assert.Empty(p3.PieceJointes);
            Assert.All(model.Plaintes.SelectMany(p => p.PieceJointes), pj => Assert.Null(pj.Fichier));
        }

        // ---------- PieceJointeFichier ----------

        private async Task AjouterPiece(PieceJointe pj)
        {
            _ctx.PieceJointes.Add(pj);
            await _ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task PieceJointeFichier_Inexistante_RenvoieNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.PieceJointeFichier(123));
        }

        [Fact]
        public async Task PieceJointeFichier_Supprimee_RenvoieNotFound()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, TypePiece = "application/pdf", TaillePiece = "1", Fichier = new byte[] { 1 }, SupprPj = true });

            Assert.IsType<NotFoundResult>(await _controller.PieceJointeFichier(1));
        }

        [Fact]
        public async Task PieceJointeFichier_SansFichier_RenvoieNotFound()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, TypePiece = "application/pdf", TaillePiece = "1", Fichier = null });

            Assert.IsType<NotFoundResult>(await _controller.PieceJointeFichier(1));
        }

        [Theory]
        [InlineData("application/pdf")]
        [InlineData("image/png")]
        [InlineData("application/zip")]
        [InlineData("text/html")]
        public async Task PieceJointeFichier_AnciennePiece_EstToujoursTelechargee(string type)
        {
            var contenu = new byte[] { 1, 2, 3 };
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "fichier.ext", TypePiece = type, TaillePiece = "3", Fichier = contenu });

            var result = Assert.IsType<FileContentResult>(await _controller.PieceJointeFichier(1));

            Assert.Equal(contenu, result.FileContents);
            Assert.StartsWith("attachment;", _controller.Response.Headers.ContentDisposition.ToString());
            Assert.Equal("nosniff", _controller.Response.Headers.XContentTypeOptions.ToString());
        }

        [Fact]
        public async Task PieceJointeFichier_TypeInvalide_UtiliseOctetStream_EtTelecharge()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "x", TypePiece = "pas un type", TaillePiece = "1", Fichier = new byte[] { 1 } });

            var result = Assert.IsType<FileContentResult>(await _controller.PieceJointeFichier(1));

            Assert.Equal("application/octet-stream", result.ContentType);
            Assert.StartsWith("attachment;", _controller.Response.Headers.ContentDisposition.ToString());
        }

        [Fact]
        public async Task PieceJointeFichier_NomEncodeDansLEntete()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "rapport été.pdf", TypePiece = "application/pdf", TaillePiece = "1", Fichier = new byte[] { 1 } });

            await _controller.PieceJointeFichier(1);

            Assert.Equal("attachment; filename*=UTF-8''rapport%20%C3%A9t%C3%A9.pdf", _controller.Response.Headers.ContentDisposition.ToString());
        }

        [Fact]
        public async Task PieceJointeFichier_SansLibelle_UtiliseLeNumeroCommeNom()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 42, NumPlainte = 1, LibPiece = "  ", TypePiece = "application/pdf", TaillePiece = "1", Fichier = new byte[] { 1 } });

            await _controller.PieceJointeFichier(42);

            Assert.Contains("piece-42", _controller.Response.Headers.ContentDisposition.ToString());
        }

        // Nouvelles pièces : fichier sur disque (CheminPiece).
        private async Task<PieceJointe> AjouterPieceSurDisque(string nom, int numPiece = 1)
        {
            var stocke = await _storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), nom, 1);
            var pj = new PieceJointe { NumPiece = numPiece, NumPlainte = 1, LibPiece = nom, TypePiece = "x", TaillePiece = "3 o", CheminPiece = stocke.RelativePath, HashPiece = stocke.Sha256 };
            await AjouterPiece(pj);
            return pj;
        }

        [Theory]
        [InlineData("facture.pdf", "application/pdf", "inline")]
        [InlineData("photo.png", "image/png", "inline")]
        [InlineData("courrier.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "attachment")]
        public async Task PieceJointeFichier_NouvellePiece_SeLitSurLeDisque(string nom, string type, string disposition)
        {
            await AjouterPieceSurDisque(nom);

            var result = Assert.IsType<FileStreamResult>(await _controller.PieceJointeFichier(1));
            await using var flux = result.FileStream;

            Assert.Equal(type, result.ContentType);
            Assert.Equal(3, flux.Length);
            Assert.StartsWith(disposition + ";", _controller.Response.Headers.ContentDisposition.ToString());
        }

        [Fact]
        public async Task PieceJointeFichier_NouvellePiece_FichierAbsentDuDisque_RenvoieNotFound()
        {
            var pj = await AjouterPieceSurDisque("a.pdf");
            File.Delete(Path.Combine(_racine, pj.CheminPiece!));

            Assert.IsType<NotFoundResult>(await _controller.PieceJointeFichier(1));
        }

        [Fact]
        public async Task PieceJointeFichier_CheminHorsRacine_RenvoieNotFound()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "a.pdf", TypePiece = "x", TaillePiece = "1", CheminPiece = "../../etc/passwd" });

            Assert.IsType<NotFoundResult>(await _controller.PieceJointeFichier(1));
        }

        // ---------- ModifierPlainte (GET et POST) ----------

        private async Task AjouterPlainteComplete()
        {
            var cite = new Cite { NumCite = 1, LibCite = "Les Pins", Secteur = "Nord" };
            var nuisance = new Nuisance { CodeNuisance = "N1", CodeSousCat = "S1", LibNuisance = "Musique" };
            _ctx.AddRange(cite, nuisance, new NaturePiece { CodeNature = "FACT", LibNature = "Facture" });
            _ctx.Plaintes.AddRange(
                new Plainte
                {
                    NumPlainte = 1, NumReclamation = "123-456", CodEtat = "Ec", CodeFait = "AUTR", PrecisionFait = "Autre fait", CodePartieCommune = "PC1",
                    NumCite = 1, CodeRue = 5, NumRue = "12", DateDeb = new DateOnly(2026, 1, 1), DateFin = new DateOnly(2026, 1, 3),
                    Commentaire = "Bruit", CodeCat = "C1", NumCiteNavigation = cite
                },
                new Plainte { NumPlainte = 2, NumReclamation = "200-2", CodEtat = "Ec", CodePartieCommune = "PC1", CodeCat = "C1", Suppr = true });
            _ctx.Composers.AddRange(
                new Composer { NumDetail = 1, NumPlainte = 1, CodeNuisance = "N1", CodeNuisanceNavigation = nuisance, Cout = 80 },
                new Composer { NumDetail = 2, NumPlainte = 1, CodeNuisance = "N1", CodeNuisanceNavigation = nuisance, Cout = 9999, SupprDet = true });
            _ctx.PieceJointes.AddRange(
                new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "facture.pdf", TypePiece = "application/pdf", TaillePiece = "2 Ko", CodeNature = "FACT", Fichier = new byte[] { 1 } },
                new PieceJointe { NumPiece = 2, NumPlainte = 1, LibPiece = "retiree.pdf", TypePiece = "application/pdf", TaillePiece = "1 Ko", SupprPj = true },
                new PieceJointe { NumPiece = 3, NumPlainte = 1, LibPiece = null, TypePiece = "image/png", TaillePiece = "3 Ko" });
            await _ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task ModifierPlainte_Inexistante_OuSupprimee_RenvoieNotFound()
        {
            await AjouterPlainteComplete();

            Assert.IsType<NotFoundResult>(await _controller.ModifierPlainte(99));
            Assert.IsType<NotFoundResult>(await _controller.ModifierPlainte(2));
        }

        [Fact]
        public async Task ModifierPlainte_Get_RemplitLeFormulaireAvecLesDonneesDeLaPlainte()
        {
            await AjouterPlainteComplete();

            var view = Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1));
            var model = Assert.IsType<CreatePlainteViewModel>(view.Model);

            Assert.Equal("CreatePlainte", view.ViewName);
            Assert.True(model.EstModification);
            Assert.Equal(1, model.NumPlainte);
            Assert.Equal("123-456", model.NumReclamation);
            Assert.Equal("AUTR", model.CodeFait);
            Assert.Equal("Autre fait", model.PrecisionFait);
            Assert.Equal("PC1", model.CodePartieCommune);
            Assert.Equal("Nord", model.Secteur);
            Assert.Equal(1, model.NumCite);
            Assert.Equal(5, model.CodeRue);
            Assert.Equal("12", model.NumRue);
            Assert.Equal(new DateOnly(2026, 1, 1), model.DateDeb);
            Assert.Equal(new DateOnly(2026, 1, 3), model.DateFin);
            Assert.Equal("Bruit", model.Commentaire);
            Assert.Equal("C1", model.CodeCat);
        }

        [Fact]
        public async Task ModifierPlainte_Get_ChargeLesDetailsNonSupprimes_AvecLaSousCategorie()
        {
            await AjouterPlainteComplete();

            var model = (CreatePlainteViewModel)Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1)).Model!;

            var detail = Assert.Single(model.Details);
            Assert.Equal("N1", detail.CodeNuisance);
            Assert.Equal("S1", detail.CodeSousCat);
            Assert.Equal(80, detail.Cout);
        }

        [Fact]
        public async Task ModifierPlainte_Get_ListeLesPiecesNonSupprimees_SansLeurContenu()
        {
            await AjouterPlainteComplete();

            var model = (CreatePlainteViewModel)Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1)).Model!;

            Assert.Equal(new[] { 1, 3 }, model.PiecesExistantes.Select(p => p.NumPiece));
            Assert.Equal("facture.pdf", model.PiecesExistantes[0].Nom);
            Assert.Equal("Facture", model.PiecesExistantes[0].Nature);
            Assert.Equal("2 Ko", model.PiecesExistantes[0].Taille);
            Assert.Equal("Pièce 3", model.PiecesExistantes[1].Nom);     // pas de libellé : numéro de la pièce
            Assert.Empty(model.Pieces);                                  // les nouvelles pièces ne sont jamais préremplies
        }

        [Fact]
        public async Task ModifierPlainte_Post_Valide_EnregistreEtRedirigeVersLaListe()
        {
            await AjouterPlainteComplete();

            var result = await _controller.ModifierPlainte(1, new CreatePlainteViewModel { NumPlainte = 999 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dossiers", redirect.ActionName);
            Assert.Equal(1, _srv.Modifie!.NumPlainte);   // le n° vient de l'adresse, jamais de ce que le formulaire envoie
        }

        [Fact]
        public async Task ModifierPlainte_Post_ErreurDeConformite_ReaffichePageSansEnregistrer()
        {
            await AjouterPlainteComplete();
            _srv.ErreurValidation = "La date de début est obligatoire.";

            var view = Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1, new CreatePlainteViewModel()));
            var model = Assert.IsType<CreatePlainteViewModel>(view.Model);

            Assert.Null(_srv.Modifie);
            Assert.Equal("CreatePlainte", view.ViewName);
            Assert.Equal("La date de début est obligatoire.", model.Erreur);
            Assert.Equal(new[] { 1, 3 }, model.PiecesExistantes.Select(p => p.NumPiece));   // rechargées pour réafficher la liste
        }

        [Fact]
        public async Task ModifierPlainte_Post_ChampInvalide_ReaffichePageSansEnregistrer()
        {
            await AjouterPlainteComplete();
            _controller.ModelState.AddModelError("DateDeb", "La date de début est obligatoire.");

            var view = Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1, new CreatePlainteViewModel()));

            Assert.Null(_srv.Modifie);
            Assert.Equal("La date de début est obligatoire.", ((CreatePlainteViewModel)view.Model!).Erreur);
        }

        [Fact]
        public async Task ModifierPlainte_Post_EchecDEnregistrement_AfficheUneErreur()
        {
            await AjouterPlainteComplete();
            _srv.ResultatModification = false;

            var view = Assert.IsType<ViewResult>(await _controller.ModifierPlainte(1, new CreatePlainteViewModel()));

            Assert.Contains("n'a pas pu être modifiée", ((CreatePlainteViewModel)view.Model!).Erreur);
        }

        [Fact]
        public async Task ModifierPlainte_Post_PlainteDevenueInexistante_RenvoieNotFound()
        {
            _srv.ErreurValidation = "Ce dossier n'existe plus.";

            Assert.IsType<NotFoundResult>(await _controller.ModifierPlainte(1, new CreatePlainteViewModel()));
            Assert.Null(_srv.Modifie);
        }
    }
}
