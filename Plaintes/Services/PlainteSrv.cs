using Microsoft.Identity.Client;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Plaintes.Services
{
    public class PlainteSrv : IPlainteSrv
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlainteSrv> _logger;
        public PlainteSrv(AppDbContext context, ILogger<PlainteSrv> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static IOrderedQueryable<T> Trier<T, TKey>(IQueryable<T> query, Expression<Func<T, TKey>> keySelector, string sens)
        {
            if (sens == "asc")
            {
                return query.OrderBy(keySelector);
            }
            else
            {
                return query.OrderByDescending(keySelector);
            }
        }

        private static IOrderedQueryable<Plainte> AppliquerTri(IQueryable<Plainte> query, string tri, string sens)
        {
            IOrderedQueryable<Plainte> tries = tri?.ToLowerInvariant() switch
            {
                "numplainte" => Trier(query, p => p.NumPlainte, sens),
                "cite" => Trier(query, p => p.NumCiteNavigation.LibCite, sens),
                "rue" => Trier(query, p => p.CodeRueNavigation.LibRue, sens),
                "partiecommune" => Trier(query, p => p.CodePartieCommuneNavigation.LibPartieCommune, sens),
                "entree" => Trier(query, p => p.NumEntree, sens),
                "reclamation" => Trier(query, p => p.NumReclamation, sens),
                "cout" => Trier(query, p => p.Composers.Where(c => !c.SupprDet).Sum(c => c.Cout), sens),
                "categorie" => Trier(query, p => p.CodeCatNavigation.LibCat, sens),
                "datecreation" => Trier(query, p => p.DateCreation, sens),
                "datedeb" => Trier(query, p => p.DateDeb, sens),
                _ => Trier(query, p => p.DateCreation, sens),
            };

            return tries.ThenByDescending(p => p.NumPlainte);
        }

        public async Task<List<Plainte>> getPlaintes()
        {
            return await _context.Plaintes
                .Where(p => !p.Suppr)
                .AsSplitQuery()
                .Include(p => p.IdUserNavigation)
                .Include(p => p.NumCiteNavigation)
                .Include(p => p.CodeRueNavigation)
                .Include(p => p.CodePartieCommuneNavigation)
                .Include(p => p.CodeCatNavigation)
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                        .ThenInclude(n => n.CodeSousCatNavigation)
                .OrderByDescending(p => p.DateCreation)
                .ThenByDescending(p => p.DateDeb)
                .ThenByDescending(p => p.NumPlainte)
                .ToListAsync();
        }

        public async Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize)
        {
            PagedResult<Plainte> result = new PagedResult<Plainte>();

            var allPlaintes = _context.Plaintes.Where(p => !p.Suppr);

            result.TotalCount = await allPlaintes.CountAsync();
            result.Plaintes = await allPlaintes
                .AsSplitQuery()
                .Include(p => p.IdUserNavigation)
                .Include(p => p.NumCiteNavigation)
                .Include(p => p.CodeRueNavigation)
                .Include(p => p.CodePartieCommuneNavigation)
                .Include(p => p.CodeCatNavigation)
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                        .ThenInclude(n => n.CodeSousCatNavigation)
                .OrderByDescending(p => p.DateCreation)
                .ThenByDescending(p => p.DateDeb)
                .ThenByDescending(p => p.NumPlainte)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            result.Page = pageNumber;

            result.PageSize = pageSize;
            return result;
        }

        public async Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize)
        {
            PagedResult<Plainte> result = new PagedResult<Plainte>();
            var query = _context.Plaintes.Where(p => !p.Suppr);
            if (filtres.numPlainte.HasValue)
            {
                query = query.Where(p => p.NumPlainte == filtres.numPlainte.Value);
            }
            if (!string.IsNullOrEmpty(filtres.cite))
            {
                query = query.Where(p => p.NumCiteNavigation.LibCite.Contains(filtres.cite));
            }
            if (!string.IsNullOrEmpty(filtres.rue))
            {
                query = query.Where(p => p.CodeRueNavigation.LibRue.Contains(filtres.rue));
            }
            if (!string.IsNullOrEmpty(filtres.partieCommune))
            {
                query = query.Where(p => p.CodePartieCommuneNavigation.LibPartieCommune.Contains(filtres.partieCommune));
            }
            if (!string.IsNullOrEmpty(filtres.entree))
            {
                query = query.Where(p => p.NumEntree.Contains(filtres.entree));
            }
            if (!string.IsNullOrEmpty(filtres.reclamation))
            {
                query = query.Where(p => p.NumReclamation.Contains(filtres.reclamation));
            }
            if (!string.IsNullOrEmpty(filtres.categorie))
            {
                query = query.Where(p => p.CodeCat == filtres.categorie);
            }
            if (!string.IsNullOrEmpty(filtres.sousCategorie))
            {
                query = query.Where(p => p.Composers.Any(c => c.CodeNuisanceNavigation.CodeSousCat == filtres.sousCategorie));
            }
            if (!string.IsNullOrEmpty(filtres.nuisance))
            {
                query = query.Where(p => p.Composers.Any(c => c.CodeNuisance == filtres.nuisance));
            }
            if (filtres.coutMin.HasValue)
            {
                query = query.Where(p => p.Composers.Any(c => c.Cout >= filtres.coutMin.Value));
            }
            if (filtres.coutMax.HasValue)
            {
                query = query.Where(p => p.Composers.Any(c => c.Cout <= filtres.coutMax.Value));
            }
            result.TotalCount = await query.CountAsync();
            result.Page = pageNumber;
            result.PageSize = pageSize;

            query = AppliquerTri(query, tri, sens);

            result.Plaintes = await query
                .AsSplitQuery()
                .Include(p => p.IdUserNavigation)
                .Include(p => p.NumCiteNavigation)
                .Include(p => p.CodeRueNavigation)
                .Include(p => p.CodePartieCommuneNavigation)
                .Include(p => p.CodeCatNavigation)
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                        .ThenInclude(n => n.CodeSousCatNavigation)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return result;
        }

        //public async Task<Plainte> getPlainteByNumPlaintAsync(int numPlainte)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.FirstOrDefault(p => p.NumPlainte == numPlainte);
        //}

        //public async Task<List<Plainte>> getPlaintesByCite(string cite)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.NumCiteNavigation.LibCite.Contains(cite)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByRue(string rue)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.CodeRueNavigation.LibRue.Contains(rue)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByVille(string ville)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.CodePartieCommuneNavigation.LibPartieCommune.Contains(ville)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByEntree(string entree)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.NumEntree.Contains(entree)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByReclamation(string reclamation)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.NumReclamation.Contains(reclamation)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByCategorie(string codeCat)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.CodeCat == codeCat).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesBySousCat(string codeSousCat)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.Composers.Any(c => c.CodeNuisanceNavigation.CodeSousCat == codeSousCat)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByNuisance(string codeNuisance)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.Composers.Any(c => c.CodeNuisance == codeNuisance)).ToList();
        //}

        //public async Task<List<Plainte>> getPlaintesByCout(int min, int max)
        //{
        //    var plaintes = await getPlaintes();
        //    return plaintes.Where(p => p.Composers.Any(c => c.Cout >= min && c.Cout <= max)).ToList();
        //}
    }
}
