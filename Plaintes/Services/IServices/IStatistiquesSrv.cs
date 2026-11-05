using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IStatistiquesSrv
    {
        Task<StatistiquesResultat> GetStatistiques(StatistiquesFiltres filtres);
        Task<StatistiquesOptions> GetOptions();
        Task<List<FiltreExport>> DecrireFiltres(StatistiquesFiltres filtres);
    }
}
