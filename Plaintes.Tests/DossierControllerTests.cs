using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Controllers;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;

namespace Plaintes.Tests
{
    public class DossierControllerTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();
        private readonly FakePlainteSrv _srv = new();
        private readonly DossierController _controller;

        public DossierControllerTests()
        {
            _controller = new DossierController(NullLogger<DossierController>.Instance, _ctx, _srv)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void Dispose() => _ctx.Dispose();

        /// <summary>Faux service : mémorise les paramètres reçus et renvoie un résultat prédéfini.</summary>
        private sealed class FakePlainteSrv : IPlainteSrv
        {
            public PagedResult<Plainte> Resultat { get; set; } = new();
            public PlainteFiltres? Filtres { get; private set; }
            public string? Tri { get; private set; }
            public string? Sens { get; private set; }
            public int Page { get; private set; }
            public int PageSize { get; private set; }

            public Task<List<Plainte>> getPlaintes() => Task.FromResult(Resultat.Plaintes);

            public Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize) => Task.FromResult(Resultat);

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
        [InlineData("application/pdf", "inline")]
        [InlineData("image/png", "inline")]
        [InlineData("IMAGE/JPEG", "inline")]
        [InlineData("application/zip", "attachment")]
        [InlineData("text/html", "attachment")]
        public async Task PieceJointeFichier_AfficheDansLeNavigateurSeulementPdfEtImages(string type, string disposition)
        {
            var contenu = new byte[] { 1, 2, 3 };
            await AjouterPiece(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "fichier.ext", TypePiece = type, TaillePiece = "3", Fichier = contenu });

            var result = Assert.IsType<FileContentResult>(await _controller.PieceJointeFichier(1));

            Assert.Equal(contenu, result.FileContents);
            Assert.StartsWith(disposition + ";", _controller.Response.Headers.ContentDisposition.ToString());
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

            Assert.Equal("inline; filename*=UTF-8''rapport%20%C3%A9t%C3%A9.pdf", _controller.Response.Headers.ContentDisposition.ToString());
        }

        [Fact]
        public async Task PieceJointeFichier_SansLibelle_UtiliseLeNumeroCommeNom()
        {
            await AjouterPiece(new PieceJointe { NumPiece = 42, NumPlainte = 1, LibPiece = "  ", TypePiece = "application/pdf", TaillePiece = "1", Fichier = new byte[] { 1 } });

            await _controller.PieceJointeFichier(42);

            Assert.Contains("piece-42", _controller.Response.Headers.ContentDisposition.ToString());
        }
    }
}
