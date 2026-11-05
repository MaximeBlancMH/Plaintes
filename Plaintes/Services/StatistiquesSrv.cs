using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Globalization;

namespace Plaintes.Services
{
    public class StatistiquesSrv : IStatistiquesSrv
    {
        /// <summary>Libellé (et code de filtre) des cités sans secteur</summary>
        public const string NonRenseigne = "Non renseigné";

        private const int TailleClassement = 8;
        // Jusqu'à 24 mois, l'évolution est donnée mois par mois ; au-delà, année par année
        private const int MoisParMoisJusqua = 24;
        // La carte des cités a la place d'en montrer davantage que les autres classements
        private const int TailleCites = 15;
        private const int TaillePart = 6;
        private readonly AppDbContext _context;

        public StatistiquesSrv(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Fonction qui calcule les statistiques des dossiers non supprimés, avec les filtres choisis.
        /// La période porte sur la date de début des faits (DateDeb). Quand une sous-catégorie ou une nuisance est choisie, seuls les détails
        /// correspondants comptent dans les coûts et dans le classement des nuisances.
        /// </summary>
        /// <param name="f">Filtres choisis (tous facultatifs)</param>
        public async Task<StatistiquesResultat> GetStatistiques(StatistiquesFiltres f)
        {
            var actives = _context.Plaintes.Where(p => !p.Suppr);
            var r = new StatistiquesResultat();

            // Tous les filtres sauf la période (sert aux bornes du curseur, au calendrier et à la comparaison), puis avec la période
            var sansPeriode = Filtrer(actives, f);

            // Bornes du curseur : premier et dernier mois des dossiers qui répondent aux autres filtres
            var dateMin = await sansPeriode.MinAsync(p => (DateOnly?)p.DateDeb);
            var dateMax = await sansPeriode.MaxAsync(p => (DateOnly?)p.DateDeb);
            r.PeriodeMin = dateMin?.ToString("yyyy-MM");
            r.PeriodeMax = dateMax?.ToString("yyyy-MM");

            var debutChoisi = LireMois(f.Debut);
            var finChoisie = LireMois(f.Fin);
            var plaintes = sansPeriode;
            if (debutChoisi.HasValue)
                plaintes = plaintes.Where(p => p.DateDeb >= debutChoisi.Value);
            if (finChoisie.HasValue)
            {
                var dernierJour = finChoisie.Value.AddMonths(1).AddDays(-1);
                plaintes = plaintes.Where(p => p.DateDeb <= dernierJour);
            }
            var details = Details(plaintes, f);

            r.Total = await plaintes.CountAsync();
            r.NbDetails = await details.CountAsync();
            r.CoutTotal = await details.SumAsync(c => (decimal?)c.Cout) ?? 0;

            // ---------- Évolution des dossiers et des coûts ----------
            var dates = await plaintes.Select(p => p.DateDeb).ToListAsync();
            var couts = await details.Select(c => new { c.NumPlainteNavigation.DateDeb, c.Cout }).ToListAsync();

            // Mois affichés : la période choisie, ramenée aux mois où il y a des dossiers
            if (dateMin.HasValue && dateMax.HasValue)
            {
                var premier = PremierDuMois(dateMin.Value);
                var dernier = PremierDuMois(dateMax.Value);
                if (debutChoisi.HasValue && debutChoisi.Value > premier) premier = debutChoisi.Value;
                if (finChoisie.HasValue && finChoisie.Value < dernier) dernier = finChoisie.Value;
                var mois = new List<DateOnly>();
                for (var m = premier; m <= dernier; m = m.AddMonths(1)) mois.Add(m);

                r.NbMois = mois.Count;
                r.Mensuel = mois.Count <= MoisParMoisJusqua;
                r.Evolution = Serie(dates.Select(d => (d, 1m)), mois, r.Mensuel);
                r.EvolutionCout = Serie(couts.Select(c => (c.DateDeb, (decimal)(c.Cout ?? 0))), mois, r.Mensuel);

                if (r.Mensuel && mois.Count <= 12 && (debutChoisi.HasValue || finChoisie.HasValue))
                {
                    // Période choisie d'un an au plus : on la compare aux mêmes mois un an avant (mêmes filtres)
                    var debutPrec = premier.AddMonths(-12);
                    var finPrec = dernier.AddMonths(-11).AddDays(-1);
                    var precedentes = sansPeriode.Where(p => p.DateDeb >= debutPrec && p.DateDeb <= finPrec);
                    var datesPrec = await precedentes.Select(p => p.DateDeb).ToListAsync();
                    var coutsPrec = await Details(precedentes, f).Select(c => new { c.NumPlainteNavigation.DateDeb, c.Cout }).ToListAsync();
                    // Décalés d'un an pour tomber sur les mois de la période affichée
                    r.EvolutionPrecedente = Serie(datesPrec.Select(d => (d.AddYears(1), 1m)), mois, true);
                    r.EvolutionCoutPrecedente = Serie(coutsPrec.Select(c => (c.DateDeb.AddYears(1), (decimal)(c.Cout ?? 0))), mois, true);
                }
            }

            // ---------- Calendrier : toutes les années, avec les autres filtres ----------
            r.Calendrier = (await sansPeriode.Select(p => p.DateDeb).ToListAsync())
                .GroupBy(d => (d.Year, d.Month))
                .Select(g => new CaseCalendrier(g.Key.Year, g.Key.Month, g.Count()))
                .OrderBy(c => c.Annee).ThenBy(c => c.Mois).ToList();

            // ---------- Lieux ----------
            // Une seule requête de comptage par cité, qui sert aux secteurs et au classement des cités
            var parCite = await plaintes.GroupBy(p => p.NumCite).Select(g => new { NumCite = g.Key, N = g.Count() }).ToListAsync();
            var cites = await _context.Cites.AsNoTracking().ToDictionaryAsync(c => c.NumCite);

            r.Cites = parCite
                .Select(x => new StatLigne(
                    cites.TryGetValue(x.NumCite, out var c) && !string.IsNullOrWhiteSpace(c.LibCite) ? c.LibCite : "Cité " + x.NumCite,
                    x.N, x.NumCite.ToString()))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).Take(TailleCites).ToList();

            r.Secteurs = parCite
                .GroupBy(x => Secteur(cites.TryGetValue(x.NumCite, out var c) ? c.Secteur : null))
                .Select(g => new StatLigne(g.Key, g.Sum(x => x.N), g.Key))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).ToList();

            var parRue = await plaintes.GroupBy(p => p.CodeRue).Select(g => new { Code = g.Key, N = g.Count() })
                .OrderByDescending(x => x.N).ThenBy(x => x.Code).Take(TailleClassement).ToListAsync();
            var rues = await _context.Rues.AsNoTracking().ToDictionaryAsync(x => x.CodeRue);
            r.Rues = parRue
                .Select(x => new StatLigne(rues.TryGetValue(x.Code, out var rue) && !string.IsNullOrWhiteSpace(rue.LibRue) ? rue.LibRue : "Rue " + x.Code, x.N, x.Code.ToString()))
                .ToList();

            var parPc = await plaintes.GroupBy(p => p.CodePartieCommune).Select(g => new { Code = g.Key, N = g.Count() }).ToListAsync();
            var pcs = await _context.PartieCommunes.AsNoTracking().ToDictionaryAsync(x => x.CodePartieCommune);
            r.PartiesCommunes = AvecAutres(parPc
                .Select(x => new StatLigne(pcs.TryGetValue(x.Code, out var pc) && !string.IsNullOrWhiteSpace(pc.LibPartieCommune) ? pc.LibPartieCommune : x.Code, x.N, x.Code)));

            // ---------- Types : signalements par sous-catégorie, puis par nuisance ----------
            var parSousCat = await details.GroupBy(c => c.CodeNuisanceNavigation.CodeSousCat).Select(g => new { Code = g.Key, N = g.Count() }).ToListAsync();
            var sousCats = await _context.SousCategories.AsNoTracking().ToDictionaryAsync(s => s.CodeSousCat);
            r.SousCategories = AvecAutres(parSousCat
                .Select(x => new StatLigne(sousCats.TryGetValue(x.Code, out var sc) && !string.IsNullOrWhiteSpace(sc.LibSousCat) ? sc.LibSousCat : x.Code, x.N, x.Code)));

            var parNuisance = await details.GroupBy(c => c.CodeNuisance).Select(g => new { Code = g.Key, N = g.Count() })
                .OrderByDescending(x => x.N).ThenBy(x => x.Code).Take(TailleClassement).ToListAsync();
            var nuisances = await _context.Nuisances.AsNoTracking().ToDictionaryAsync(n => n.CodeNuisance);
            r.Nuisances = parNuisance
                .Select(x => new StatLigne(nuisances.TryGetValue(x.Code, out var n) && !string.IsNullOrWhiteSpace(n.LibNuisance) ? n.LibNuisance : x.Code, x.N, x.Code))
                .ToList();

            // ---------- Statistiques de remplacement ----------
            var adresses = await plaintes.GroupBy(p => new { p.CodeRue, p.NumRue }).Select(g => new { g.Key.CodeRue, g.Key.NumRue, N = g.Count() })
                .OrderByDescending(x => x.N).ThenBy(x => x.CodeRue).ThenBy(x => x.NumRue).Take(TailleClassement).ToListAsync();
            r.Adresses = adresses
                .Select(x => new StatLigne((string.IsNullOrWhiteSpace(x.NumRue) ? "" : x.NumRue.Trim() + " ")
                    + (rues.TryGetValue(x.CodeRue, out var rue) && !string.IsNullOrWhiteSpace(rue.LibRue) ? rue.LibRue : "Rue " + x.CodeRue), x.N))
                .ToList();

            var fr = CultureInfo.GetCultureInfo("fr-FR");
            r.Saisonnalite = Enumerable.Range(1, 12)
                .Select(m => new StatLigne(fr.TextInfo.ToTitleCase(fr.DateTimeFormat.GetMonthName(m)), dates.Count(d => d.Month == m)))
                .ToList();

            var nbPieces = await plaintes.Select(p => p.PieceJointes.Count(pj => !pj.SupprPj)).ToListAsync();
            r.PiecesJointes = Tranches(
                ("Aucune pièce", nbPieces.Count(n => n == 0)),
                ("1 pièce", nbPieces.Count(n => n == 1)),
                ("2 ou 3 pièces", nbPieces.Count(n => n is 2 or 3)),
                ("4 pièces ou plus", nbPieces.Count(n => n >= 4)));

            var coutParCite = await details.GroupBy(c => c.NumPlainteNavigation.NumCite).Select(g => new { Code = g.Key, Total = g.Sum(c => c.Cout ?? 0) }).ToListAsync();
            r.CoutCites = coutParCite.Where(x => x.Total > 0)
                .Select(x => new StatLigne(cites.TryGetValue(x.Code, out var c) && !string.IsNullOrWhiteSpace(c.LibCite) ? c.LibCite : "Cité " + x.Code, x.Total, x.Code.ToString()))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).Take(TailleClassement).ToList();

            var coutParPc = await details.GroupBy(c => c.NumPlainteNavigation.CodePartieCommune).Select(g => new { Code = g.Key, Total = g.Sum(c => c.Cout ?? 0) }).ToListAsync();
            r.CoutPartiesCommunes = coutParPc.Where(x => x.Total > 0)
                .Select(x => new StatLigne(pcs.TryGetValue(x.Code, out var pc) && !string.IsNullOrWhiteSpace(pc.LibPartieCommune) ? pc.LibPartieCommune : x.Code, x.Total, x.Code))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).Take(TailleClassement).ToList();

            var coutParNuisance = await details.GroupBy(c => c.CodeNuisance).Select(g => new { Code = g.Key, Total = g.Sum(c => c.Cout ?? 0) }).ToListAsync();
            r.CoutNuisances = coutParNuisance.Where(x => x.Total > 0)
                .Select(x => new StatLigne(nuisances.TryGetValue(x.Code, out var n) && !string.IsNullOrWhiteSpace(n.LibNuisance) ? n.LibNuisance : x.Code, x.Total, x.Code))
                .OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).Take(TailleClassement).ToList();

            return r;
        }

        /// <summary>
        /// Fonction qui transforme des tranches (libellé, nombre) en lignes de graphique, sans les tranches vides
        /// </summary>
        private static List<StatLigne> Tranches(params (string Libelle, int Nombre)[] tranches) =>
            tranches.Where(t => t.Nombre > 0).Select(t => new StatLigne(t.Libelle, t.Nombre)).ToList();

        /// <summary>
        /// Fonction qui rappelle en clair les filtres choisis (libellés des listes plutôt que leurs codes), pour l'export Excel
        /// </summary>
        /// <param name="f">Filtres choisis</param>
        public async Task<List<FiltreExport>> DecrireFiltres(StatistiquesFiltres f)
        {
            var o = await GetOptions();
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            var liste = new List<FiltreExport>();
            void Ajouter(string libelle, List<OptionFiltre> options, string? code)
            {
                if (!string.IsNullOrWhiteSpace(code))
                    liste.Add(new FiltreExport(libelle, options.FirstOrDefault(x => x.Code == code)?.Libelle ?? code));
            }

            if (LireMois(f.Debut) is DateOnly debut) liste.Add(new FiltreExport("Début des faits, à partir de", debut.ToString("MMM yyyy", fr)));
            if (LireMois(f.Fin) is DateOnly fin) liste.Add(new FiltreExport("Début des faits, jusqu'à", fin.ToString("MMM yyyy", fr)));
            Ajouter("Secteur", o.Secteurs, f.Secteur);
            Ajouter("Cité", o.Cites, f.Cite?.ToString());
            Ajouter("Partie commune", o.PartiesCommunes, f.PartieCommune);
            Ajouter("Catégorie", o.Categories, f.Categorie);
            Ajouter("Sous-catégorie", o.SousCategories, f.SousCategorie);
            Ajouter("Nuisance", o.Nuisances, f.Nuisance);
            return liste;
        }

        /// <summary>
        /// Fonction qui renvoie les valeurs proposées dans les listes de filtres (éléments non supprimés)
        /// </summary>
        public async Task<StatistiquesOptions> GetOptions()
        {
            var cites = await _context.Cites.AsNoTracking().OrderBy(c => c.LibCite).ToListAsync();
            return new StatistiquesOptions
            {
                Secteurs = cites.Select(c => Secteur(c.Secteur)).Distinct()
                    .OrderBy(s => s == NonRenseigne).ThenBy(s => s)
                    .Select(s => new OptionFiltre(s, s)).ToList(),
                Cites = cites.Select(c => new OptionFiltre(c.NumCite.ToString(), c.LibCite ?? "Cité " + c.NumCite, Secteur(c.Secteur))).ToList(),
                Categories = await _context.Categories.AsNoTracking().Where(c => !c.SupprCat).OrderBy(c => c.LibCat)
                    .Select(c => new OptionFiltre(c.CodeCat, c.LibCat ?? c.CodeCat, null)).ToListAsync(),
                SousCategories = await _context.SousCategories.AsNoTracking().Where(s => !s.SupprSousCat).OrderBy(s => s.LibSousCat)
                    .Select(s => new OptionFiltre(s.CodeSousCat, s.LibSousCat ?? s.CodeSousCat, s.CodeCat)).ToListAsync(),
                Nuisances = await _context.Nuisances.AsNoTracking().Where(n => !n.SupprNuisance).OrderBy(n => n.LibNuisance)
                    .Select(n => new OptionFiltre(n.CodeNuisance, n.LibNuisance ?? n.CodeNuisance, n.CodeSousCat)).ToListAsync(),
                PartiesCommunes = await _context.PartieCommunes.AsNoTracking().Where(p => !p.SupprPc).OrderBy(p => p.LibPartieCommune)
                    .Select(p => new OptionFiltre(p.CodePartieCommune, p.LibPartieCommune ?? p.CodePartieCommune, null)).ToListAsync()
            };
        }

        /// <summary>
        /// Fonction qui applique tous les filtres sauf l'année
        /// </summary>
        private static IQueryable<Plainte> Filtrer(IQueryable<Plainte> q, StatistiquesFiltres f)
        {
            if (!string.IsNullOrWhiteSpace(f.Secteur))
            {
                if (f.Secteur == NonRenseigne)
                    q = q.Where(p => p.NumCiteNavigation.Secteur == null || p.NumCiteNavigation.Secteur.Trim() == "");
                else
                {
                    var secteur = f.Secteur.Trim().ToUpper();
                    q = q.Where(p => p.NumCiteNavigation.Secteur != null && p.NumCiteNavigation.Secteur.Trim().ToUpper() == secteur);
                }
            }
            if (f.Cite.HasValue)
                q = q.Where(p => p.NumCite == f.Cite.Value);
            if (!string.IsNullOrWhiteSpace(f.Categorie))
                q = q.Where(p => p.CodeCat == f.Categorie);
            if (!string.IsNullOrWhiteSpace(f.SousCategorie))
                q = q.Where(p => p.Composers.Any(c => !c.SupprDet && c.CodeNuisanceNavigation.CodeSousCat == f.SousCategorie));
            if (!string.IsNullOrWhiteSpace(f.Nuisance))
                q = q.Where(p => p.Composers.Any(c => !c.SupprDet && c.CodeNuisance == f.Nuisance));
            if (!string.IsNullOrWhiteSpace(f.PartieCommune))
                q = q.Where(p => p.CodePartieCommune == f.PartieCommune);
            return q;
        }

        /// <summary>
        /// Fonction qui renvoie les détails non supprimés des dossiers donnés, limités à la sous-catégorie ou à la nuisance choisie
        /// </summary>
        private IQueryable<Composer> Details(IQueryable<Plainte> plaintes, StatistiquesFiltres f)
        {
            var details = _context.Composers.Where(c => !c.SupprDet && plaintes.Any(p => p.NumPlainte == c.NumPlainte));
            if (!string.IsNullOrWhiteSpace(f.SousCategorie))
                details = details.Where(c => c.CodeNuisanceNavigation.CodeSousCat == f.SousCategorie);
            if (!string.IsNullOrWhiteSpace(f.Nuisance))
                details = details.Where(c => c.CodeNuisance == f.Nuisance);
            return details;
        }

        /// <summary>
        /// Fonction qui additionne des valeurs datées mois par mois (sur les mois donnés) ou année par année (sur les années de ces mois)
        /// </summary>
        /// <param name="points">Valeurs datées (date de début des faits, valeur)</param>
        /// <param name="mois">Mois de la période affichée (premier jour de chaque mois)</param>
        /// <param name="mensuel">Vrai : un point par mois ; faux : un point par année</param>
        private static List<StatLigne> Serie(IEnumerable<(DateOnly Date, decimal Valeur)> points, List<DateOnly> mois, bool mensuel)
        {
            var liste = points.ToList();
            if (mensuel)
            {
                var fr = CultureInfo.GetCultureInfo("fr-FR");
                return mois
                    .Select(m => new StatLigne(fr.DateTimeFormat.GetAbbreviatedMonthName(m.Month) + " " + m.ToString("yy"),
                        (int)Math.Round(liste.Where(p => p.Date.Year == m.Year && p.Date.Month == m.Month).Sum(p => p.Valeur))))
                    .ToList();
            }

            return mois.Select(m => m.Year).Distinct()
                .Select(a => new StatLigne(a.ToString(),
                    (int)Math.Round(liste.Where(p => p.Date.Year == a && mois.Any(m => m.Year == a && m.Month == p.Date.Month)).Sum(p => p.Valeur))))
                .ToList();
        }

        /// <summary>
        /// Fonction qui lit un mois « aaaa-mm » ; renvoie le premier jour du mois, ou null si la valeur est vide ou invalide
        /// </summary>
        private static DateOnly? LireMois(string? valeur) =>
            DateOnly.TryParseExact((valeur ?? "").Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static DateOnly PremierDuMois(DateOnly d) => new(d.Year, d.Month, 1);

        /// <summary>
        /// Fonction qui trie un classement et regroupe les plus petites valeurs dans « Autres » (pour les camemberts : le total reste complet)
        /// </summary>
        private static List<StatLigne> AvecAutres(IEnumerable<StatLigne> lignes)
        {
            var triees = lignes.OrderByDescending(l => l.Valeur).ThenBy(l => l.Libelle).ToList();
            var resultat = triees.Take(TaillePart).ToList();
            if (triees.Count > TaillePart)
                resultat.Add(new StatLigne("Autres", triees.Skip(TaillePart).Sum(l => l.Valeur)));
            return resultat;
        }

        private static string Secteur(string? secteur) =>
            string.IsNullOrWhiteSpace(secteur) ? NonRenseigne : secteur.Trim().ToUpperInvariant();
    }
}
