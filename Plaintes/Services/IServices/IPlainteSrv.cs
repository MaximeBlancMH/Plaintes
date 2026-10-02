using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IPlainteSrv
    {
        //Task<Plainte> getPlainteByNumPlaintAsync(int numPlainte);
        //Task<List<Plainte>> getPlaintesByCite(string cite);
        //Task<List<Plainte>> getPlaintesByRue(string userId);
        //Task<List<Plainte>> getPlaintesByVille(string ville);
        //Task<List<Plainte>> getPlaintesByEntree(string entree);
        //Task<List<Plainte>> getPlaintesByReclamation(string reclamation);
        //Task<List<Plainte>> getPlaintesByCategorie(string codeCat);
        //Task<List<Plainte>> getPlaintesBySousCat(string codeSousCat);
        //Task<List<Plainte>> getPlaintesByNuisance(string codeNuisance);
        //Task<List<Plainte>> getPlaintesByCout(int min, int max);

        Task<List<Plainte>> getPlaintes();
        Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize);
        Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize);


    }
}
