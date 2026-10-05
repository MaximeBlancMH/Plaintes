using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Linq.Expressions;
using System.Numerics;

namespace Plaintes.Services
{
    public class PlainteSrv : IPlainteSrv
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly ILogger<PlainteSrv> _logger;
        public PlainteSrv(AppDbContext context, ILogger<PlainteSrv> logger, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _config = config;
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
                query = query.Where(p => p.Composers.Where(c => !c.SupprDet).Any(c => c.Cout >= filtres.coutMin.Value));
            }
            if (filtres.coutMax.HasValue)
            {
                query = query.Where(p => p.Composers.Where(c => !c.SupprDet).Any(c => c.Cout <= filtres.coutMax.Value));
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

        public async Task<byte[]> createPlainteFile(Dictionary<string, string> valeurs)
        {
            string path = _config["ModelePlainte"];
            using var ms = new MemoryStream();
            using (var fs = File.OpenRead(path))
                fs.CopyTo(ms);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, true))
            {
                var main = doc.MainDocumentPart!;

                var paragraphes = main.Document.Body!.Descendants<Paragraph>()
                    .Concat(main.HeaderParts.SelectMany(h => h.Header.Descendants<Paragraph>()))
                    .Concat(main.FooterParts.SelectMany(f => f.Footer.Descendants<Paragraph>()))
                    .Where(p => p.InnerText.Contains("{{"))
                    .ToList();

                foreach (var p in paragraphes)
                {
                    foreach (var kv in valeurs)
                    {
                        ReplaceTextInParagraph(p, kv.Key, kv.Value);
                    }
                }
            }

            return ms.ToArray();
        }

        private static void ReplaceTextInParagraph(Paragraph p, string key, string value)
        {
            int depart = 0;
            while(true)
            {
                var textes = p.Descendants<Text>().ToList();
                var fullText = string.Concat(textes.Select(t => t.Text));

                var firstIndex = fullText.IndexOf(key, depart, StringComparison.Ordinal);
                if (firstIndex < 0) return;
                var lastIndex = firstIndex + key.Length;

                int index = 0;
                bool firstSeen = false;
                foreach (var t in textes)
                {
                    int textBegin = index;
                    int textEnd = index + t.Text.Length;
                    index = textEnd;

                    if (textEnd <= firstIndex || textBegin >= lastIndex) continue;

                    var before = t.Text.Substring(0, Math.Max(0, firstIndex - textBegin));
                    var after = t.Text.Substring(Math.Min(t.Text.Length, lastIndex - textBegin));

                    t.Text = firstSeen ? after : before + value + after;
                    t.Space = SpaceProcessingModeValues.Preserve;
                    firstSeen = true;
                }

                index = firstIndex + value.Length;
            }
        }

        public async Task<Dictionary<string, string>> getValuesForPlainteFile(Plainte plainte)
        {
            var valeurs = new Dictionary<string, string>();

            if (plainte != null)
            {
                valeurs["{{CASE_VOLONTAIRES}}"] = "☐";
                valeurs["{{CASE_INCENDIE}}"] = "☐";
                valeurs["{{CASE_TAGS}}"] = "☐";
                valeurs["{{CASE_ORDURES}}"] = "☐";
                valeurs["{{CASE_AUTRES_FAITS}}"] = "☐";
                valeurs["{{AUTRES_FAITS}}"] = "";

                if (plainte.CodeFait == "DVOL")
                {
                    valeurs["{{CASE_VOLONTAIRES}}"] = "☒";
                }
                else if (plainte.CodeFait == "DINC")
                {
                    valeurs["{{CASE_INCENDIE}}"] = "☒";
                }
                else if (plainte.CodeFait == "DTAG")
                {
                    valeurs["{{CASE_TAGS}}"] = "☒";
                }
                else if (plainte.CodeFait == "DORD")
                {
                    valeurs["{{CASE_ORDURES}}"] = "☒";
                }
                else if (plainte.CodeFait == "DAUT")
                {
                    valeurs["{{CASE_AUTRES_FAITS}}"] = "☒";
                    valeurs["{{AUTRES_FAITS}}"] = plainte.PrecisionFait;
                }

                if (plainte.DateDeb != null)
                {
                    valeurs["{{CASE_DATE_LE}}"] = plainte.DateFin == null || plainte.DateDeb == plainte.DateFin ? "☒" : "☐";
                    valeurs["{{DATE_LE}}"] = plainte.DateFin == null || plainte.DateDeb == plainte.DateFin ? plainte.DateDeb.ToString("dd/MM/yyyy") : "";
                    valeurs["{{CASE_DATE_ENTRE}}"] = plainte.DateFin != null && plainte.DateDeb != plainte.DateFin ? "☒" : "☐";
                    valeurs["{{DATE_ENTRE}}"] = plainte.DateFin != null && plainte.DateDeb != plainte.DateFin ? $"le {plainte.DateDeb.ToString("dd/MM/yyyy")} et le {plainte.DateFin?.ToString("dd/MM/yyyy")}" : "";
                }

                valeurs["{{LIEU}}"] = $"{plainte.NumRue} {plainte.CodeRueNavigation.LibRue} à {plainte.CodeRueNavigation.Ville} ({plainte.CodeRueNavigation.Cp.Substring(0, 2)})";

                valeurs["{{EXPOSE}}"] = $"{plainte.Commentaire} (Réclamation Mâcon Habitat {plainte.NumReclamation}).";

                if (plainte.PieceJointes.Count() > 0)
                {
                    foreach (var pj in plainte.PieceJointes)
                    {
                        if (pj.CodeNature == "PHOT")
                            valeurs["{{CASE_PHOTOS}}"] = "☒";
                        else if (pj.CodeNature == "FACT")
                            valeurs["{{CASE_FACTURES}}"] = "☒";
                        else if (pj.CodeNature == "AUTR")
                        {
                            valeurs["{{CASE_AUTRES_PJ}}"] = "☒";
                            valeurs["{{AUTRES_PJ}}"] += $"{pj.PrecisionNature}\n";
                        }
                    }

                    valeurs["{{CASE_PHOTOS}}"] = "☐";
                    valeurs["{{CASE_FACTURES}}"] = "☐";
                    valeurs["{{CASE_AUTRES_PJ}}"] = "☐";
                    valeurs["{{AUTRES_PJ}}"] = "";
                }
                else
                {
                    valeurs["{{CASE_PHOTOS}}"] = "☐";
                    valeurs["{{CASE_FACTURES}}"] = "☐";
                    valeurs["{{CASE_AUTRES_PJ}}"] = "☐";
                    valeurs["{{AUTRES_PJ}}"] = "";
                }

                valeurs["{{DATE_JOUR}}"] = DateTime.Now.ToString("dd/MM/yyyy");

                return valeurs;
            }
            
            return null;
        }
    }
}
