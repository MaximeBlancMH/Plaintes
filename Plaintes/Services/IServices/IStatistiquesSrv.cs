using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IStatistiquesSrv
    {
        Task<StatistiquesViewModel> GetStatistiques(int? annee);
    }
}
