using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Controllers;
using Plaintes.Models;
using Plaintes.Services.IServices;

namespace Plaintes.Tests
{
    public class HomeControllerTests : IDisposable
    {
        private readonly Data.AppDbContext _ctx = TestDb.Creer();
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            _controller = new HomeController(NullLogger<HomeController>.Instance, _ctx, new PlainteSrvVide())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void Dispose() => _ctx.Dispose();

        private sealed class PlainteSrvVide : IPlainteSrv
        {
            public Task<List<Plainte>> getPlaintes() => Task.FromResult(new List<Plainte>());
            public Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize) => Task.FromResult(new PagedResult<Plainte>());
            public Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize) => Task.FromResult(new PagedResult<Plainte>());
            public Task<byte[]> createPlainteFile(Dictionary<string, string> valeurs) => Task.FromResult(new byte[0]);
            public Task<bool> createPlainte(CreatePlainteViewModel model, string userEmail) => Task.FromResult(true);
            public Task<bool> updatePlainte(CreatePlainteViewModel model) => throw new NotImplementedException();
            public Task<string?> validerPlainte(CreatePlainteViewModel model) => Task.FromResult<string?>(null);
            public Task AddPiecesAsync(int numPlainte, IEnumerable<PieceJointeViewModel> pieces, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Dictionary<string, string>> getValuesForPlainteFile(Plainte plainte) => Task.FromResult(new Dictionary<string, string>());
        }

        [Fact]
        public void Index_RedirigeVersLesDossiers()
        {
            var result = Assert.IsType<RedirectToActionResult>(_controller.Index());

            Assert.Equal("Dossiers", result.ActionName);
            Assert.Equal("Dossier", result.ControllerName);
        }

        [Fact]
        public void Privacy_RenvoieLaVue()
        {
            Assert.IsType<ViewResult>(_controller.Privacy());
        }

        [Fact]
        public void Error_RenvoieLaVueAvecLIdentifiantDeRequete()
        {
            _controller.HttpContext.TraceIdentifier = "trace-123";

            var view = Assert.IsType<ViewResult>(_controller.Error());

            var model = Assert.IsType<ErrorViewModel>(view.Model);
            Assert.False(string.IsNullOrEmpty(model.RequestId));
        }
    }
}
