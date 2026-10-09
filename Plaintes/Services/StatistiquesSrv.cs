using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Globalization;

namespace Plaintes.Services
{
    public class StatistiquesSrv : IStatistiquesSrv
    {
        private const int TailleClassement = 8;
        private const int TaillePart = 6;
        private readonly AppDbContext _context;

        public StatistiquesSrv(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Fonction qui calcule les statistiques des dossiers non supprimés, pour une année ou pour toutes les années.
        /// L'année est celle du début des faits (DateDeb).
        /// </summary>
        /// <param name="annee">Année choisie, null pour toutes les années</param>
        public async Task<StatistiquesViewModel> GetStatistiques(int? annee)
        {
            var actives = _context.Plaintes.Where(p => !p.Suppr);

            var vm = new StatistiquesViewModel
            {
                Annee = annee,
                Annees = (await actives.Select(p => p.DateDeb).ToListAsync())
                    .Select(d => d.Year).Distinct().OrderByDescending(a => a).ToList()
            };

            var plaintes = annee.HasValue ? actives.Where(p => p.DateDeb.Year == annee.Value) : actives;
            // Seuls les détails non supprimés des dossiers retenus comptent
            var details = _context.Composers.Where(c => !c.SupprDet && plaintes.Any(p => p.NumPlainte == c.NumPlainte));

            vm.Total = await plaintes.CountAsync();
            vm.EnCours = await plaintes.CountAsync(p => p.CodEtat == "Ec");
            vm.CoutTotal = await details.SumAsync(c => (decimal?)c.Cout) ?? 0;

            vm.Evolution = Evolution(await plaintes.Select(p => p.DateDeb).ToListAsync(), annee);
            // Pour une année choisie, on garde aussi l'année d'avant pour comparer
            if (annee.HasValue)
                vm.EvolutionPrecedente = Evolution(await actives.Where(p => p.DateDeb.Year == annee.Value - 1).Select(p => p.DateDeb).ToListAsync(), annee.Value - 1);

            // Une seule requête de comptage par cité, qui sert aux secteurs et au classement des cités
            var parCite = await plaintes.GroupBy(p => p.NumCite).Select(g => new { NumCite = g.Key, N = g.Count() }).ToListAsync();
            var cites = await _context.Cites.ToDictionaryAsync(c => c.NumCite);

            vm.Cites = parCite
                .Select(x => new StatLigne(cites.TryGetValue(x.NumCite, out var c) && !string.IsNullOrWhiteSpace(c.LibCite) ? c.LibCite : "Cité inconnue", x.N))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).Take(TailleClassement).ToList();

            vm.Secteurs = parCite
                .GroupBy(x => cites.TryGetValue(x.NumCite, out var c) && !string.IsNullOrWhiteSpace(c.Secteur) ? c.Secteur!.Trim().ToUpperInvariant() : "Non renseigné")
                .Select(g => new StatLigne(g.Key, g.Sum(x => x.N)))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).ToList();

            var parCategorie = await plaintes.GroupBy(p => p.CodeCat).Select(g => new { Code = g.Key, N = g.Count() }).ToListAsync();
            var categories = await _context.Categories.ToDictionaryAsync(c => c.CodeCat);
            var lignesCategories = parCategorie
                .Select(x => new StatLigne(categories.TryGetValue(x.Code, out var c) && !string.IsNullOrWhiteSpace(c.LibCat) ? c.LibCat : x.Code, x.N))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).ToList();
            // Pour le camembert, les plus petites catégories sont regroupées dans « Autres » : le total reste complet
            vm.Categories = lignesCategories.Take(TaillePart).ToList();
            if (lignesCategories.Count > TaillePart)
                vm.Categories.Add(new StatLigne("Autres", lignesCategories.Skip(TaillePart).Sum(l => l.Valeur)));

            var parNuisance = await details.GroupBy(c => c.CodeNuisance).Select(g => new { Code = g.Key, N = g.Count() })
                .OrderByDescending(x => x.N).ThenBy(x => x.Code).Take(TailleClassement).ToListAsync();
            var nuisances = await _context.Nuisances.ToDictionaryAsync(n => n.CodeNuisance);
            vm.Nuisances = parNuisance
                .Select(x => new StatLigne(nuisances.TryGetValue(x.Code, out var n) && !string.IsNullOrWhiteSpace(n.LibNuisance) ? n.LibNuisance : x.Code, x.N))
                .ToList();

            return vm;
        }

        /// <summary>
        /// Fonction qui compte les dossiers par mois (12 mois de l'année choisie) ou par année (toutes les années, sans trou)
        /// </summary>
        private static List<StatLigne> Evolution(List<DateOnly> dates, int? annee)
        {
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            if (annee.HasValue)
            {
                return Enumerable.Range(1, 12)
                    .Select(m => new StatLigne(fr.DateTimeFormat.GetAbbreviatedMonthName(m), dates.Count(d => d.Month == m)))
                    .ToList();
            }

            if (dates.Count == 0) return new();
            var debut = dates.Min(d => d.Year);
            var fin = dates.Max(d => d.Year);
            return Enumerable.Range(debut, fin - debut + 1)
                .Select(a => new StatLigne(a.ToString(), dates.Count(d => d.Year == a)))
                .ToList();
        }
    }
}
