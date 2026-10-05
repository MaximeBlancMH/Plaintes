using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
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
        // Code "Autre(s)" des tables type_fait et nature_piece : une précision est alors obligatoire.
        private const string CodeAutre = "AUTR";
        public PlainteSrv(AppDbContext context, ILogger<PlainteSrv> logger, IConfiguration config, IPieceStorage storage)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _storage = storage;
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

        /// Contrôle que chaque champ du formulaire est conforme à la base de données (obligatoire, longueur maximale,
        /// valeur existante dans la table liée).
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
            for (int i = 0; i < model.Pieces.Count; i++)
            {
                var piece = model.Pieces[i];
                int n = i + 1;
                if (piece.Fichier is null || piece.Fichier.Length == 0)
                    return $"Pièce jointe {n} : le fichier est vide ou manquant.";
                if (piece.NomFichier is null || piece.NomFichier.Length > 150)
                    return $"Pièce jointe {n} : le nom du fichier est absent ou dépasse 150 caractères.";
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

            return null;
        }

        /// <summary>
        /// Enregistre les pièces jointes d'une plainte : chaque fichier est écrit sur disque (IPieceStorage), puis une ligne
        /// est ajoutée dans piece_jointe avec son chemin relatif (CHEMINPIECE) et son empreinte SHA-256 (HASHPIECE) ;
        /// la colonne FICHIER reste vide. Si une étape échoue, les fichiers déjà écrits sont supprimés et l'exception est relancée.
        /// </summary>
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

        // Taille lisible, stockée dans piece_jointe.TAILLEPIECE (varchar(20)) : "850 o", "12,3 Ko", "4,5 Mo".
        private static string FormaterTaille(long octets) => octets switch
        {
            < 1024 => $"{octets} o",
            < 1024 * 1024 => $"{octets / 1024.0:0.#} Ko",
            _ => $"{octets / 1024.0 / 1024.0:0.#} Mo"
        };

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

                plainte = await _context.Plaintes
                    .FirstOrDefaultAsync(p => p.NumReclamation == plainte.NumReclamation);
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
