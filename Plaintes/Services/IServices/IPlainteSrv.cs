using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IPlainteSrv
    {
        Task<List<Plainte>> getPlaintes();
        Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize);
        Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize);
        Task<byte[]> createPlainteFile(Dictionary<string, string> valeurs);
        Task<Dictionary<string, string>> getValuesForPlainteFile(Plainte plainte);
        Task<bool> createPlainte(CreatePlainteViewModel model, string userEmail);
        Task<string?> validerPlainte(CreatePlainteViewModel model);
        Task AddPiecesAsync(int numPlainte, IEnumerable<PieceJointeViewModel> pieces, CancellationToken ct = default);
    }
}
