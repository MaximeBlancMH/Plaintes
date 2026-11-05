using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using Plaintes.Services.Stockage;
using System.Linq.Expressions;
using System.Numerics;

namespace Plaintes.Services
{
    public class PlainteSrv : IPlainteSrv
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly ILogger<PlainteSrv> _logger;
        private readonly IPieceStorage _storage;
        private readonly StorageSettings _reglagesStockage;
        // Code "Autre(s)" des tables type_fait et nature_piece : une précision est alors obligatoire.
        private const string CodeAutre = "AUTR";
        public PlainteSrv(AppDbContext context, ILogger<PlainteSrv> logger, IConfiguration config, IPieceStorage storage, IOptions<StorageSettings> reglagesStockage)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _storage = storage;
            _reglagesStockage = reglagesStockage.Value;
        }

        /// <summary>
        /// Fonction qui ordonne les objets de type T selon la colonne et le sens donnés
        /// </summary>
        /// <typeparam name="T">Objet parent du champ de tri. Par exemple : Plainte</typeparam>
        /// <typeparam name="TKey">Type de champ de tri. Par exemple : string</typeparam>
        /// <param name="query">Requête à trier</param>
        /// <param name="keySelector">Champ qui va être ordonné. Par exemple : plainte.NumPlainte</param>
        /// <param name="sens">Ordre de tri</param>
        /// <returns>Requête non exécutée qui renvoie plusieurs objets de type T (Donc par exemple Plainte)</returns>
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

        /// <summary>
        /// Fonction qui va appliquer la mise en ordre des plaintes selon la colonne et le sens choisis en appelant une fonction selon la colonne.
        /// </summary>
        /// <param name="query">Requête à trier</param>
        /// <param name="tri">Colonne qui doit être mise en ordre</param>
        /// <param name="sens">Ordre de tri</param>
        /// <returns>Requête non exécutée qui renvoie des Plaintes</returns>
        private static IOrderedQueryable<Plainte> AppliquerTri(IQueryable<Plainte> query, string tri, string sens)
        {
            IOrderedQueryable<Plainte> tries = tri?.ToLowerInvariant() switch
            {
                "numplainte" => Trier(query, p => p.NumPlainte, sens),
                "cite" => Trier(query, p => p.NumCiteNavigation.LibCite, sens),
                "rue" => Trier(query, p => p.CodeRueNavigation.LibRue, sens),
                "partiecommune" => Trier(query, p => p.CodePartieCommuneNavigation.LibPartieCommune, sens),
                "reclamation" => Trier(query, p => p.NumReclamation, sens),
                "cout" => Trier(query, p => p.Composers.Where(c => !c.SupprDet).Sum(c => c.Cout), sens),
                "categorie" => Trier(query, p => p.CodeCatNavigation.LibCat, sens),
                "datecreation" => Trier(query, p => p.DateCreation, sens),
                "datedeb" => Trier(query, p => p.DateDeb, sens),
                _ => Trier(query, p => p.DateCreation, sens),
            };

            return tries.ThenByDescending(p => p.NumPlainte);
        }

        /// <summary>
        /// Fonction qui récupère les plaintes filtrées et triées en fonction du champ et de l'ordre et qui renvoie ces plaintes en 
        /// fonction du nombre de plaintes affichées par page et la page actuelle.
        /// </summary>
        /// <param name="filtres">Tous les filtres renseignés</param>
        /// <param name="tri">Champ de tri</param>
        /// <param name="sens"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns>Les plaintes triées et ordonnées</returns>
        public async Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize)
        {
            PagedResult<Plainte> result = new PagedResult<Plainte>();
            var query = FiltrerSansPeriode(filtres);

            // Bornes du curseur de période, avant d'appliquer la période elle-même : elles se resserrent avec les autres filtres
            result.PeriodeMin = await query.MinAsync(p => (DateOnly?)p.DateDeb);
            result.PeriodeMax = await query.MaxAsync(p => (DateOnly?)p.DateDeb);
            query = FiltrerPeriode(query, filtres);

            if (pageNumber < 1) pageNumber = 1;

            result.TotalCount = await query.CountAsync();
            result.Page = pageNumber;
            result.PageSize = pageSize;
            result.CoutMax = await query.SelectMany(p => p.Composers).Where(c => !c.SupprDet).MaxAsync(c => (int?)c.Cout) ?? 0;

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

        /// <summary>
        /// Fonction qui récupère, pour l'export Excel, toutes les plaintes qui répondent aux filtres de la page Dossiers, dans l'ordre du tri choisi
        /// (sans pagination), avec le nombre de pièces jointes de chacune et le rappel des filtres
        /// </summary>
        /// <param name="filtres">Filtres de la page (les mêmes que l'affichage)</param>
        /// <param name="tri">Colonne de tri</param>
        /// <param name="sens">Sens du tri</param>
        public async Task<DossiersExport> GetPlaintesPourExport(PlainteFiltres filtres, string tri, string sens)
        {
            var query = FiltrerPeriode(FiltrerSansPeriode(filtres), filtres);

            var plaintes = await AppliquerTri(query, tri, sens)
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.IdUserNavigation)
                .Include(p => p.NumCiteNavigation)
                .Include(p => p.CodeRueNavigation)
                .Include(p => p.CodePartieCommuneNavigation)
                .Include(p => p.CodeCatNavigation)
                .Include(p => p.CodeFaitNavigation)
                .Include(p => p.CodEtatNavigation)
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                        .ThenInclude(n => n.CodeSousCatNavigation)
                .ToListAsync();

            // Nombre de pièces jointes (sans charger les pièces elles-mêmes)
            var nbPieces = await query
                .Select(p => new { p.NumPlainte, N = p.PieceJointes.Count(pj => !pj.SupprPj) })
                .ToDictionaryAsync(x => x.NumPlainte, x => x.N);

            return new DossiersExport(plaintes, nbPieces, await DecrireFiltres(filtres, tri, sens));
        }

        /// <summary>
        /// Fonction qui rappelle en clair les filtres et le tri choisis (libellés des listes plutôt que leurs codes), pour l'export Excel
        /// </summary>
        private async Task<List<FiltreExport>> DecrireFiltres(PlainteFiltres f, string tri, string sens)
        {
            var liste = new List<FiltreExport>();
            void Ajouter(string libelle, string? valeur)
            {
                if (!string.IsNullOrWhiteSpace(valeur)) liste.Add(new FiltreExport(libelle, valeur.Trim()));
            }

            Ajouter("N° de dossier", f.numPlainte?.ToString());
            Ajouter("Cité (contient)", f.cite);
            Ajouter("Rue (contient)", f.rue);
            Ajouter("Partie commune (contient)", f.partieCommune);
            Ajouter("N° de réclamation (contient)", f.reclamation);
            Ajouter("Commentaire (contient)", f.commentaire);
            if (!string.IsNullOrEmpty(f.categorie))
                Ajouter("Catégorie", (await _context.Categories.FindAsync(f.categorie))?.LibCat ?? f.categorie);
            if (!string.IsNullOrEmpty(f.sousCategorie))
                Ajouter("Sous-catégorie", (await _context.SousCategories.FindAsync(f.sousCategorie))?.LibSousCat ?? f.sousCategorie);
            if (!string.IsNullOrEmpty(f.nuisance))
                Ajouter("Nuisance", (await _context.Nuisances.FindAsync(f.nuisance))?.LibNuisance ?? f.nuisance);
            Ajouter("Coût d'un détail, au moins", f.coutMin.HasValue ? f.coutMin + " €" : null);
            Ajouter("Coût d'un détail, au plus", f.coutMax.HasValue ? f.coutMax + " €" : null);
            Ajouter("Début des faits, à partir de", LibelleMois(f.debut));
            Ajouter("Début des faits, jusqu'à", LibelleMois(f.fin));
            if (!string.IsNullOrEmpty(tri))
                Ajouter("Tri", (ColonnesTri.TryGetValue(tri.ToLowerInvariant(), out var colonne) ? colonne : tri)
                    + (sens == "asc" ? " (croissant)" : " (décroissant)"));
            return liste;
        }

        // Libellés des colonnes de tri (voir AppliquerTri)
        private static readonly Dictionary<string, string> ColonnesTri = new()
        {
            ["numplainte"] = "N° de dossier", ["cite"] = "Cité", ["rue"] = "Rue", ["partiecommune"] = "Partie commune",
            ["reclamation"] = "N° de réclamation", ["cout"] = "Coût", ["categorie"] = "Catégorie",
            ["datecreation"] = "Date de saisie", ["datedeb"] = "Début des faits"
        };

        /// <summary>
        /// Fonction qui écrit un mois « aaaa-mm » en clair (« janv. 2026 ») ; null si la valeur est vide ou invalide
        /// </summary>
        private static string? LibelleMois(string? mois) =>
            LireMois(mois) is DateOnly d ? d.ToString("MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) : null;

        /// <summary>
        /// Fonction qui lit un mois « aaaa-mm » ; renvoie le premier jour du mois, ou null si la valeur est vide ou invalide
        /// </summary>
        private static DateOnly? LireMois(string? mois) =>
            DateOnly.TryParseExact(mois + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;

        /// <summary>
        /// Fonction qui applique tous les filtres de la page Dossiers sauf la période (plaintes non supprimées)
        /// </summary>
        private IQueryable<Plainte> FiltrerSansPeriode(PlainteFiltres filtres)
        {
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
            if (!string.IsNullOrEmpty(filtres.reclamation))
            {
                query = query.Where(p => p.NumReclamation.Contains(filtres.reclamation));
            }
            if (!string.IsNullOrEmpty(filtres.commentaire))
            {
                query = query.Where(p => p.Commentaire != null && p.Commentaire.Contains(filtres.commentaire));
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
            return query;
        }

        /// <summary>
        /// Fonction qui garde les plaintes dont la date de début des faits est dans la période choisie (premier et dernier mois inclus)
        /// </summary>
        private static IQueryable<Plainte> FiltrerPeriode(IQueryable<Plainte> query, PlainteFiltres filtres)
        {
            if (LireMois(filtres.debut) is DateOnly premierJour)
            {
                query = query.Where(p => p.DateDeb >= premierJour);
            }
            if (LireMois(filtres.fin) is DateOnly debutDernierMois)
            {
                var dernierJour = debutDernierMois.AddMonths(1).AddDays(-1);
                query = query.Where(p => p.DateDeb <= dernierJour);
            }
            return query;
        }

        /// <summary>
        /// Fonction qui va remplir le template d'une plainte par les valeurs attendues puis regénérer le docx
        /// </summary>
        /// <param name="valeurs">Valeurs de chaque champ du template plainte</param>
        /// <returns>Les octets du fichier</returns>
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

        /// <summary>
        /// Fonction qui va remplacer les mots-clef du template par leur contenus. On est obligé de concaténer en paragraphe car Word découpe mal les mots
        /// </summary>
        /// <param name="p">Paragraph</param>
        /// <param name="key">Mot-clef à remplacer</param>
        /// <param name="value">Valeur qui va remplacer le mot-clef</param>
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

                depart = firstIndex + value.Length; // on reprend après la valeur insérée (évite de re-remplacer ou de boucler)
            }
        }

        /// <summary>
        /// Création du tableau de valeurs pour le template plainte. On remplit les champs en fonction du contenu de la plainte
        /// </summary>
        /// <param name="plainte">Objet plainte qui contient les données qu'on va mettre dans notre document</param>
        /// <returns>Dictionnaire avec un texte en clef et en valeur. Exemple : "{{DATE_JOUR}}" avec "06/10/2026"</returns>
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
                else if (plainte.CodeFait == "TAGS")
                {
                    valeurs["{{CASE_TAGS}}"] = "☒";
                }
                else if (plainte.CodeFait == "DORD")
                {
                    valeurs["{{CASE_ORDURES}}"] = "☒";
                }
                else if (plainte.CodeFait == "AUTR")
                {
                    valeurs["{{CASE_AUTRES_FAITS}}"] = "☒";
                    valeurs["{{AUTRES_FAITS}}"] = plainte.PrecisionFait ?? "";
                }

                if (plainte.DateDeb != null)
                {
                    valeurs["{{CASE_DATE_LE}}"] = plainte.DateFin == null || plainte.DateDeb == plainte.DateFin ? "☒" : "☐";
                    valeurs["{{DATE_LE}}"] = plainte.DateFin == null || plainte.DateDeb == plainte.DateFin ? plainte.DateDeb.ToString("dd/MM/yyyy") : "";
                    valeurs["{{CASE_DATE_ENTRE}}"] = plainte.DateFin != null && plainte.DateDeb != plainte.DateFin ? "☒" : "☐";
                    valeurs["{{DATE_ENTRE}}"] = plainte.DateFin != null && plainte.DateDeb != plainte.DateFin ? $"le {plainte.DateDeb.ToString("dd/MM/yyyy")} et le {plainte.DateFin?.ToString("dd/MM/yyyy")}" : "";
                }

                var rue = plainte.CodeRueNavigation;
                var departement = rue?.Cp?.Trim() is { Length: >= 2 } cp ? cp.Substring(0, 2) : "";
                var voie = $"{plainte.NumRue?.Trim()} {rue?.LibRue?.Trim()}".Trim();
                var commune = string.IsNullOrWhiteSpace(rue?.Ville) ? "" : $"à {rue.Ville.Trim()}";
                var suffixe = departement == "" ? "" : $"({departement})";
                valeurs["{{LIEU}}"] = $"{voie} {commune} {suffixe}".Trim();

                valeurs["{{EXPOSE}}"] = $"{plainte.Commentaire?.Trim()} (Réclamation Mâcon Habitat {plainte.NumReclamation}).".Trim();

                if (plainte.PieceJointes.Count() > 0)
                {
                    valeurs["{{CASE_PHOTOS}}"] = "☐";
                    valeurs["{{CASE_FACTURES}}"] = "☐";
                    valeurs["{{CASE_AUTRES_PJ}}"] = "☐";
                    valeurs["{{AUTRES_PJ}}"] = "";

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

        /// <summary>
        /// Fonction qui vérifie que le model qui va servir à la création ou la modification d'une plainte en base de données
        /// respecte toutes les règles de cette dernière.
        /// </summary>
        /// <param name="model">Model qui contient toutes les données à ajouter</param>
        /// <returns>Null s'il n'y a pas d'erreur, l'intitulé de l'erreur sinon</returns>
        public async Task<string?> validerPlainte(CreatePlainteViewModel model)
        {
            // ---------- N° de réclamation : NUMRECLAMATION varchar(12) NOT NULL ----------
            if (string.IsNullOrWhiteSpace(model.NumReclamation))
                return "Le n° de réclamation est obligatoire.";
            if (model.NumReclamation.Length > 12)
                return "Le n° de réclamation ne peut pas dépasser 12 caractères.";

            // ---------- Type de faits : CODEFAIT varchar(4) NULL, clé étrangère vers type_fait ----------
            if (!string.IsNullOrEmpty(model.CodeFait))
            {
                if (model.CodeFait.Length > 4 || !await _context.TypeFaits.AnyAsync(t => t.CodeFait == model.CodeFait))
                    return "Le type de faits choisi n'existe pas.";
            }
            // PRECISIONFAIT varchar(250) NULL : obligatoire quand le type de faits est "Autres".
            if (model.CodeFait == CodeAutre && string.IsNullOrWhiteSpace(model.PrecisionFait))
                return "Précisez le type de faits (« Autres »).";
            if (model.PrecisionFait is { Length: > 250 })
                return "La précision du type de faits ne peut pas dépasser 250 caractères.";

            // ---------- Patrimoine ----------
            // CODEPARTIECOMMUNE varchar(4) NOT NULL
            if (string.IsNullOrEmpty(model.CodePartieCommune))
                return "La partie commune est obligatoire.";
            if (model.CodePartieCommune.Length > 4 ||
                !await _context.PartieCommunes.AnyAsync(p => p.CodePartieCommune == model.CodePartieCommune && !p.SupprPc))
                return "La partie commune choisie n'existe pas.";

            // NUMCITE int NOT NULL
            if (model.NumCite is null)
                return "La cité est obligatoire.";
            if (!await _context.Cites.AnyAsync(c => c.NumCite == model.NumCite))
                return "La cité choisie n'existe pas.";

            // CODERUE int NOT NULL ; NUMRUE varchar(50) NULL
            if (model.CodeRue is null)
                return "L'adresse est obligatoire.";
            if (!await _context.Rues.AnyAsync(r => r.CodeRue == model.CodeRue))
                return "La rue choisie n'existe pas.";
            if (model.NumRue is { Length: > 50 })
                return "Le numéro de rue ne peut pas dépasser 50 caractères.";
            if (!await _context.BatimentRues.AnyAsync(br => br.CodeRue == model.CodeRue
                    && br.NumRue == (model.NumRue ?? "") && br.IdBatNavigation.NumCite == model.NumCite))
                return "Cette adresse n'appartient pas à la cité choisie.";

            // ---------- Période : DATEDEB date NOT NULL, DATEFIN date NULL ----------
            if (model.DateDeb is null)
                return "La date de début est obligatoire.";
            if (model.DateFin is not null && model.DateFin < model.DateDeb)
                return "La date de fin ne peut pas être antérieure à la date de début.";

            // ---------- Commentaire : COMMENTAIRE varchar(250) NULL ----------
            if (model.Commentaire is { Length: > 250 })
                return "Le commentaire ne peut pas dépasser 250 caractères.";

            // ---------- Catégorie : CODECAT varchar(4) NOT NULL, clé étrangère vers categorie ----------
            if (string.IsNullOrEmpty(model.CodeCat))
                return "La catégorie est obligatoire.";
            if (model.CodeCat.Length > 4 || !await _context.Categories.AnyAsync(c => c.CodeCat == model.CodeCat && !c.SupprCat))
                return "La catégorie choisie n'existe pas.";

            // ---------- Détails : composer (CODENUISANCE varchar(5) NOT NULL, COUT int NULL) ----------
            for (int i = 0; i < model.Details.Count; i++)
            {
                var detail = model.Details[i];
                int n = i + 1;
                if (string.IsNullOrEmpty(detail.CodeNuisance))
                    return $"Détail {n} : la nuisance est obligatoire.";
                var nuisance = await _context.Nuisances.AsNoTracking()
                    .Where(x => x.CodeNuisance == detail.CodeNuisance && !x.SupprNuisance)
                    .Select(x => new { x.CodeSousCat, x.CodeSousCatNavigation.CodeCat })
                    .FirstOrDefaultAsync();
                if (detail.CodeNuisance.Length > 5 || nuisance is null)
                    return $"Détail {n} : la nuisance choisie n'existe pas.";
                if (nuisance.CodeCat != model.CodeCat)
                    return $"Détail {n} : la nuisance ne correspond pas à la catégorie choisie.";
                if (!string.IsNullOrEmpty(detail.CodeSousCat) && detail.CodeSousCat != nuisance.CodeSousCat)
                    return $"Détail {n} : la nuisance ne correspond pas à la sous-catégorie choisie.";
                if (detail.Cout is < 0)
                    return $"Détail {n} : le coût ne peut pas être négatif.";
            }

            // ---------- Pièces jointes : piece_jointe (LIBPIECE varchar(150), TYPEPIECE varchar(90) NOT NULL,
            //            FICHIER varbinary(max), CODENATURE varchar(4) NULL, PRECISIONNATURE varchar(100) NULL) ----------
            // Modification : les pièces déjà enregistrées (et non retirées) comptent dans la limite.
            var piecesConservees = 0;
            if (model.EstModification)
            {
                if (!await _context.Plaintes.AnyAsync(p => p.NumPlainte == model.NumPlainte && !p.Suppr))
                    return "Ce dossier n'existe plus.";
                piecesConservees = await _context.PieceJointes.CountAsync(pj => pj.NumPlainte == model.NumPlainte
                    && !pj.SupprPj && !model.PiecesASupprimer.Contains(pj.NumPiece));
            }
            if (piecesConservees + model.Pieces.Count > _reglagesStockage.MaxPieces)
                return $"Vous ne pouvez pas joindre plus de {_reglagesStockage.MaxPieces} pièces.";
            for (int i = 0; i < model.Pieces.Count; i++)
            {
                var piece = model.Pieces[i];
                int n = i + 1;
                if (piece.Fichier is null || piece.Fichier.Length == 0)
                    return $"Pièce jointe {n} : le fichier est vide ou manquant.";
                if (piece.NomFichier is null || piece.NomFichier.Length > 150)
                    return $"Pièce jointe {n} : le nom du fichier est absent ou dépasse 150 caractères.";
                if (!_reglagesStockage.ExtensionAutorisee(piece.Extension))
                    return $"Pièce jointe {n} : le type de fichier « {(piece.Extension == "" ? "sans extension" : piece.Extension)} » n'est pas autorisé (autorisés : {string.Join(", ", _reglagesStockage.Extensions)}).";
                if (piece.Fichier.Length > _reglagesStockage.MaxSize)
                    return $"Pièce jointe {n} : le fichier fait {FormaterTaille(piece.Fichier.Length)}, la taille maximale est de {FormaterTaille(_reglagesStockage.MaxSize)}.";
                if (string.IsNullOrEmpty(piece.Fichier.ContentType) || piece.Fichier.ContentType.Length > 90)
                    return $"Pièce jointe {n} : le type du fichier est invalide.";
                if (string.IsNullOrEmpty(piece.CodeNature))
                    return $"Pièce jointe {n} : la nature de la pièce est obligatoire.";
                if (piece.CodeNature.Length > 4 || !await _context.NaturePieces.AnyAsync(x => x.CodeNature == piece.CodeNature))
                    return $"Pièce jointe {n} : la nature choisie n'existe pas.";
                if (piece.CodeNature == CodeAutre && string.IsNullOrWhiteSpace(piece.PrecisionNature))
                    return $"Pièce jointe {n} : précisez la nature de la pièce (« Autre »).";
                if (piece.PrecisionNature is { Length: > 100 })
                    return $"Pièce jointe {n} : la précision ne peut pas dépasser 100 caractères.";
            }

            var tailleTotale = model.Pieces.Sum(p => p.Fichier?.Length ?? 0);
            if (tailleTotale > _reglagesStockage.MaxTotalSize)
                return $"Les pièces jointes font {FormaterTaille(tailleTotale)} au total, le maximum est de {FormaterTaille(_reglagesStockage.MaxTotalSize)}.";
            return null;
        }

        /// <summary>
        /// Enregistre chaque piece jointe sur le disque puis ses métadonnées dans la base de données.
        /// </summary>
        /// <param name="numPlainte">Numéro de la plainte</param>
        /// <param name="pieces">Liste des pièces jointes à ajouter</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns></returns>
        public async Task AddPiecesAsync(int numPlainte, IEnumerable<PieceJointeViewModel> pieces, CancellationToken ct = default)
        {
            var ecrits = new List<string>();
            try
            {
                foreach (var piece in pieces)
                {
                    if (piece.Fichier is null || piece.NomFichier is null)
                        throw new InvalidOperationException("Pièce jointe sans fichier.");

                    await using var flux = piece.Fichier.OpenReadStream();
                    var stocke = await _storage.SaveAsync(flux, piece.NomFichier, numPlainte, ct: ct);
                    ecrits.Add(stocke.RelativePath);

                    _context.PieceJointes.Add(new PieceJointe
                    {
                        NumPlainte = numPlainte,
                        LibPiece = piece.NomFichier,
                        TypePiece = piece.Fichier.ContentType,
                        TaillePiece = FormaterTaille(stocke.Size),
                        CheminPiece = stocke.RelativePath,
                        HashPiece = stocke.Sha256,
                        Fichier = null,
                        CodeNature = piece.CodeNature,
                        PrecisionNature = piece.CodeNature == CodeAutre ? piece.PrecisionNature?.Trim() : null
                    });
                }
                await _context.SaveChangesAsync(ct);
            }
            catch
            {
                // Les lignes non enregistrées ne doivent pas rester suivies, et les fichiers orphelins sont retirés du disque.
                foreach (var entree in _context.ChangeTracker.Entries<PieceJointe>()
                             .Where(e => e.State == EntityState.Added).ToList())
                    entree.State = EntityState.Detached;
                foreach (var chemin in ecrits)
                {
                    try { _storage.Delete(chemin); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Impossible de supprimer le fichier orphelin {Chemin}.", chemin); }
                }
                throw;
            }
        }

        /// <summary>
        /// On formate la taille selon le nombre d'octets
        /// </summary>
        /// <param name="octets">Nombre d'octets</param>
        /// <returns>String qui contient le nombre d'octets formatés</returns>
        private static string FormaterTaille(long octets) => octets switch
        {
            < 1024 => $"{octets} o",
            < 1024 * 1024 => $"{octets / 1024.0:0.#} Ko",
            _ => $"{octets / 1024.0 / 1024.0:0.#} Mo"
        };

        /// <summary>
        /// Enregistre la modification d'une plainte (après validerPlainte). On utilise une transaction 
        /// pour qu'un détail supprimé ne le reste pas si la fin de la modification échoue par exemple.
        /// </summary>
        /// <param name="model">Model qui contient les données de la plainte à modifier</param>
        /// <returns>Un booléen qui indique la réussite ou non de l'opération</returns>
        public async Task<bool> updatePlainte(CreatePlainteViewModel model)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var plainte = await _context.Plaintes
                    .Include(p => p.Composers)
                    .Include(p => p.PieceJointes)
                    .FirstOrDefaultAsync(p => p.NumPlainte == model.NumPlainte && !p.Suppr);
                if (plainte == null) return false;

                plainte.NumReclamation = model.NumReclamation!;
                plainte.CodeFait = model.CodeFait;
                plainte.PrecisionFait = model.CodeFait == CodeAutre ? model.PrecisionFait?.Trim() : null;
                plainte.CodePartieCommune = model.CodePartieCommune!;
                plainte.NumCite = model.NumCite.GetValueOrDefault();
                plainte.CodeRue = model.CodeRue.GetValueOrDefault();
                plainte.NumRue = model.NumRue?.Trim();
                plainte.DateDeb = model.DateDeb.GetValueOrDefault();
                plainte.DateFin = model.DateFin;
                plainte.Commentaire = model.Commentaire;
                plainte.CodeCat = model.CodeCat!;
                plainte.DateModification = DateTime.Now;   // la valeur par défaut de la base ne joue qu'à la création

                foreach (var ancien in plainte.Composers.Where(c => !c.SupprDet))
                    ancien.SupprDet = true;
                foreach (var detail in model.Details)
                    plainte.Composers.Add(new Composer { CodeNuisance = detail.CodeNuisance!, Cout = detail.Cout });

                // Seules les pièces de cette plainte peuvent être retirées, quels que soient les numéros reçus.
                foreach (var pj in plainte.PieceJointes.Where(pj => !pj.SupprPj && model.PiecesASupprimer.Contains(pj.NumPiece)))
                    pj.SupprPj = true;

                await _context.SaveChangesAsync();
                await AddPiecesAsync(plainte.NumPlainte, model.Pieces);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la modification de la plainte {NumPlainte}.", model.NumPlainte);
                return false;
            }
        }

        /// <summary>
        /// Fonction qui crée une plainte en base de données. On utilise l'email de l'utilisateur afin 
        /// de récupérer son id qui est la clef primaire en base de données.
        /// </summary>
        /// <param name="model">Model qui contient toutes les données de la plainte à créer</param>
        /// <param name="userEmail">Email de l'utilisateur qui a posté la plainte.</param>
        /// <returns>Un booléen qui atteste de la réussite ou non de la méthode</returns>
        public async Task<bool> createPlainte(CreatePlainteViewModel model, string userEmail)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var plainte = new Plainte
                {
                    NumReclamation = model.NumReclamation,
                    CodEtat = "Ec",
                    CodeFait = model.CodeFait,
                    PrecisionFait = model.CodeFait == CodeAutre ? model.PrecisionFait?.Trim() : null,
                    CodePartieCommune = model.CodePartieCommune,
                    NumCite = model.NumCite.GetValueOrDefault(),
                    CodeRue = model.CodeRue.GetValueOrDefault(),
                    NumRue = model.NumRue?.Trim(),
                    DateDeb = model.DateDeb.GetValueOrDefault(),
                    DateFin = model.DateFin,
                    Commentaire = model.Commentaire,
                    CodeCat = model.CodeCat,
                    DateCreation = DateTime.Now,
                    Suppr = false,
                    IdUser = _context.Utilisateurs.FirstOrDefault(u => u.UserEmail == userEmail)?.IdUser ?? 0,
                };
                _context.Plaintes.Add(plainte);
                await _context.SaveChangesAsync();

                foreach (var detail in model.Details)
                {
                    var composer = new Composer
                    {
                        NumPlainte = plainte.NumPlainte,
                        CodeNuisance = detail.CodeNuisance,
                        Cout = detail.Cout
                    };
                    _context.Composers.Add(composer);
                }
                // Détails enregistrés d'abord, puis les pièces : fichiers sur disque + une ligne par pièce en base.
                await _context.SaveChangesAsync();
                await AddPiecesAsync(plainte.NumPlainte, model.Pieces);

                await transaction.CommitAsync();

                return true;
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la plainte.");
                return false;
            }
        }
    }
}
