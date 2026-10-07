using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.Export;
using Plaintes.Services.IServices;
using Plaintes.Services.Stockage;
using System.Security.Claims;

namespace Plaintes.Controllers
{
    [Route("Dossiers")]
    public class DossierController : Controller
    {
        private readonly ILogger<DossierController> _logger;
        private readonly AppDbContext _context;
        private readonly IPlainteSrv _plainteSrv;
        private readonly IPieceStorage _storage;

        public DossierController(ILogger<DossierController> logger, AppDbContext context, IPlainteSrv plainteSrv, IPieceStorage storage)
        {
            _logger = logger;
            _context = context;
            _plainteSrv = plainteSrv;
            _storage = storage;
        }

        /// <summary>
        /// Action qui récupère les dossiers plainte et les renvoie dans la vue correspondante. 
        /// On récupère seulement le nombre de plaintes demandé par la pagination et qui correspondent aux filtres sélectionnés
        /// </summary>
        /// <param name="filtres">Filtres sélectionné sur la page</param>
        /// <param name="tri">Colonne à trier pour l'ordre des plaintes</param>
        /// <param name="sens">Sens du tri</param>
        /// <param name="page">Numéro de page</param>
        /// <param name="pageSize">Nombre de plaintes affichées par page</param>
        /// <returns></returns>
        [Authorize]
        public async Task<IActionResult> Dossiers(PlainteFiltres filtres, string tri, string sens, int page = 1, int pageSize = 10)
        {
            if (pageSize is not (10 or 25 or 50)) pageSize = 10;

            // On récupère les plaintes selon les filtres
            PagedResult<Plainte> result = await _plainteSrv.getPlaintesWithFilters(filtres, tri, sens, page, pageSize);

            // On récupère uniquement les plaintes de la page demandée, avec leurs relations sauf les pièces jointes.
            // On charge les pièces jointes sans leurs fichiers pour garder la liste légère.
            var numeros = result.Plaintes.Select(p => p.NumPlainte).ToList();
            var pieces = await _context.PieceJointes
                .AsNoTracking()
                .Where(pj => !pj.SupprPj && numeros.Contains(pj.NumPlainte))
                .Select(pj => new PieceJointe
                {
                    NumPiece = pj.NumPiece,
                    NumPlainte = pj.NumPlainte,
                    LibPiece = pj.LibPiece,
                    TypePiece = pj.TypePiece,
                    TaillePiece = pj.TaillePiece
                })
                .ToListAsync();

            // On associe les pièces jointes aux plaintes.
            var piecesParPlainte = pieces.ToLookup(pj => pj.NumPlainte);
            foreach (var p in result.Plaintes)
                p.PieceJointes = piecesParPlainte[p.NumPlainte].ToList();

            return View(result);
        }

        /// <summary>
        /// Récupère les propositions possibles pour les listes déroulantes et affiche la page de création d'une plainte
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpGet("Creer")]
        public async Task<IActionResult> Saisie()
        {
            var model = new CreatePlainteViewModel();
            await RemplirListes(model);
            return View("CreatePlainte", model);
        }


        /// <summary>
        /// Action de type POST qui contrôle les informations saisies puis ajoute la plainte en base de données
        /// </summary>
        /// <param name="model">Model contenant les informations saisies de la nouvelle plainte</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("Creer")]
        [ServiceFilter(typeof(LimiteEnvoiPiecesFilter), Order = int.MinValue)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePlainteViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Première erreur de saisie (champ obligatoire, format...) affichée au-dessus du formulaire.
                model.Erreur = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Une valeur saisie est invalide." : e.ErrorMessage)
                    .First();
            }
            else
            {
                try
                {
                    // On contrôle la conformité des champs avec la base avant d'enregistrer.
                    model.Erreur = await _plainteSrv.validerPlainte(model);
                    if (model.Erreur == null)
                    {
                        // On ajoute la plainte dans la base de données
                        string userEmail = User.FindFirstValue(ClaimTypes.Email) ?? throw new InvalidOperationException("Utilisateur non authentifié.");
                        bool result = await _plainteSrv.createPlainte(model, userEmail);
                        if (result)
                        {
                            return RedirectToAction(nameof(Dossiers));
                        }
                        model.Erreur = "La plainte n'a pas pu être enregistrée.";
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Si une exception est levée, on la rajoute dans l'état du model et on re-remplit les listes déroulantes pour l'affichage
                    ModelState.AddModelError(string.Empty, ex.Message);
                    await RemplirListes(model);
                    return View("CreatePlainte", model);
                }
            }

            // On remplit de nouveau les listes avant de recharger la page
            await RemplirListes(model);
            return View("CreatePlainte", model);
        }

        /// <summary>
        /// Fonction qui permet de remplir les listes déroulantes pour le formulaire de création ou la modification de plainte
        /// </summary>
        /// <param name="model">Model de données pour la page de création de plaintes</param>
        /// <returns></returns>
        private async Task RemplirListes(CreatePlainteViewModel model)
        {
            model.TypesFait = await _context.TypeFaits.AsNoTracking().OrderBy(t => t.LibFait).ToListAsync();
            model.PartiesCommunes = await _context.PartieCommunes.AsNoTracking().Where(p => !p.SupprPc).OrderBy(p => p.LibPartieCommune).ToListAsync();
            model.Cites = await _context.Cites.AsNoTracking().OrderBy(c => c.LibCite)
                .Select(c => new CiteChoix(c.NumCite, c.LibCite, c.Secteur)).ToListAsync();
            model.Categories = await _context.Categories.AsNoTracking().Where(c => !c.SupprCat).OrderBy(c => c.LibCat).ToListAsync();
            model.SousCategories = await _context.SousCategories.AsNoTracking().Where(s => !s.SupprSousCat).OrderBy(s => s.LibSousCat)
                .Select(s => new SousCategorieChoix(s.CodeSousCat, s.CodeCat, s.LibSousCat)).ToListAsync();
            model.Nuisances = await _context.Nuisances.AsNoTracking().Where(n => !n.SupprNuisance).OrderBy(n => n.LibNuisance)
                .Select(n => new NuisanceChoix(n.CodeNuisance, n.CodeSousCat, n.LibNuisance)).ToListAsync();
            model.Natures = await _context.NaturePieces.AsNoTracking().OrderBy(n => n.LibNature).ToListAsync();
            model.Secteurs = model.Cites.Where(c => !string.IsNullOrWhiteSpace(c.Secteur))
                .Select(c => c.Secteur!).Distinct().OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Action qui récupère l'adresse de tous les bâtiments d'une cité
        /// </summary>
        /// <param name="numCite">Numéro de la cité</param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("Creer/Rues")]
        public async Task<IActionResult> RuesParCite(int numCite)
        {
            // On récupère toutes les informations de l'adresse grâce à la table Rue
            var adresses = await _context.BatimentRues.AsNoTracking()
                .Where(br => br.IdBatNavigation.NumCite == numCite)
                .Select(br => new { br.CodeRue, br.NumRue, Lib = br.CodeRueNavigation.LibRue, br.CodeRueNavigation.Cp, br.CodeRueNavigation.Ville })
                .Distinct()
                .OrderBy(a => a.Lib).ThenBy(a => a.NumRue)
                .ToListAsync();
            
            return Json(adresses.Select(a => new
            {
                // "codeRue|numRue" : le formulaire en tire CodeRue et NumRue.
                valeur = $"{a.CodeRue}|{a.NumRue}",
                libelle = string.Join(" ", new[] { a.NumRue, a.Lib, a.Cp, a.Ville }.Where(s => !string.IsNullOrWhiteSpace(s)))
            }));
        }

        /// <summary>
        /// Action qui ouvre la pièce jointe dans le navigateur pour certains types et télécharge pour d'autres
        /// </summary>
        /// <param name="id">Id de la pièce jointe</param>
        /// <returns></returns>
        [Authorize]
        [Route("PiecesJointes/{id:int}")]
        public async Task<IActionResult> PieceJointeFichier(int id)
        {
            // On récupère la pièce jointe dans la base de données.
            var pj = await _context.PieceJointes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NumPiece == id && !x.SupprPj);
            if (pj == null) return NotFound();

            // On récupère le nom de la pièce jointe ou on génère un nom avec son numéro s'il n'existe pas.
            var nom = string.IsNullOrWhiteSpace(pj.LibPiece) ? $"piece-{pj.NumPiece}" : pj.LibPiece;
            Response.Headers.XContentTypeOptions = "nosniff";

            if (!string.IsNullOrEmpty(pj.CheminPiece))
            {
                Stream flux;
                try
                {
                    // On regarde si le chemin stocké en base existe toujours. Si oui, on ouvre notre fichier
                    if (!_storage.Exists(pj.CheminPiece))
                    {
                        _logger.LogWarning("Le fichier de la pièce jointe {PieceJointeId} n'existe pas dans le stockage.", id);
                        return NotFound();
                    }
                    flux = _storage.Open(pj.CheminPiece);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    _logger.LogError(ex, "Impossible d'ouvrir le fichier de la pièce jointe {PieceJointeId}.", id);
                    return NotFound();
                }

                // On déduit le type du fichier grâce à son extension
                new FileExtensionContentTypeProvider().TryGetContentType(nom, out var typeDisque);
                typeDisque ??= "application/octet-stream";
                var inline = typeDisque == "application/pdf" || typeDisque.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                Response.Headers.ContentDisposition =
                    $"{(inline ? "inline" : "attachment")}; filename*=UTF-8''{Uri.EscapeDataString(nom)}";
                return File(flux, typeDisque);
            }

            // On a gardé des anciennes pièces jointes dont le contenu est stocké dans la base de données, on la télécharge directement
            if (pj.Fichier == null) return NotFound();
            var type = Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(pj.TypePiece, out var parsed)
                ? parsed.MediaType.Value!
                : "application/octet-stream";
            Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(nom)}";
            return File(pj.Fichier, type);
        }

        /// <summary>
        /// Action qui supprime une plainte (on passe la valeur Suppr à true)
        /// </summary>
        /// <param name="id">Id de la plainte à supprimer</param>
        /// <returns></returns>
        [Authorize]
        [Route("{id:int}/delete")]
        public async Task<IActionResult> SupprimerPlainte(int id)
        {
            // On récupère la plainte dans la base de données.
            var plainte = await _context.Plaintes.FindAsync(id);
            if (plainte == null || plainte.Suppr)
            {
                return NotFound();
            }

            // Marquer la plainte comme supprimée
            plainte.Suppr = true;

            // On sauvegarde les changements dans la base de données.
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Dossiers));
        }

        /// <summary>
        /// Action qui récupère la plainte à modifier et l'envoie avec la vue de modification
        /// </summary>
        /// <param name="id">Id de la plainte à modifier</param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("{id:int}/update")]
        public async Task<IActionResult> ModifierPlainte(int id)
        {
            // On récupère la plainte dans la base de données (les pièces sont lues à part, sans leur contenu).
            var plainte = await _context.Plaintes
                .AsNoTracking()
                .Include(p => p.NumCiteNavigation)
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                .FirstOrDefaultAsync(p => p.NumPlainte == id && !p.Suppr);
            if (plainte == null)
            {
                return NotFound();
            }

            // On prépare le modèle pour la vue de modification.
            var model = new CreatePlainteViewModel
            {
                NumPlainte = plainte.NumPlainte,
                NumReclamation = plainte.NumReclamation,
                CodeFait = plainte.CodeFait,
                PrecisionFait = plainte.PrecisionFait,
                CodePartieCommune = plainte.CodePartieCommune,
                Secteur = plainte.NumCiteNavigation?.Secteur,
                NumCite = plainte.NumCite,
                CodeRue = plainte.CodeRue,
                NumRue = plainte.NumRue,
                DateDeb = plainte.DateDeb,
                DateFin = plainte.DateFin,
                Commentaire = plainte.Commentaire,
                CodeCat = plainte.CodeCat
            };

            // On ajoute chaque détail à notre view model
            foreach (var det in plainte.Composers)
            {
                model.Details.Add(new DetailPlainteViewModel
                {
                    CodeNuisance = det.CodeNuisance,
                    CodeSousCat = det.CodeNuisanceNavigation?.CodeSousCat,
                    Cout = det.Cout
                });
            }

            // On récupère les pièces jointes ainsi que les listes déroulantes
            await ChargerPiecesExistantes(model);
            await RemplirListes(model);
            return View("CreatePlainte", model);
        }

        /// <summary>
        /// Action de type POST qui contrôle le view model d'une plainte envoyé et modifie la plainte concernée ensuite
        /// </summary>
        /// <param name="id">Id de la plainte à modifier</param>
        /// <param name="model">View Model de la plainte qui contient ses nouvelles données</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("{id:int}/update")]
        [ServiceFilter(typeof(LimiteEnvoiPiecesFilter), Order = int.MinValue)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifierPlainte(int id, CreatePlainteViewModel model)
        {
            model.NumPlainte = id;

            if (!ModelState.IsValid)
            {
                model.Erreur = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Une valeur saisie est invalide." : e.ErrorMessage)
                    .First();
            }
            else
            {
                model.Erreur = await _plainteSrv.validerPlainte(model);
                if (model.Erreur == "Ce dossier n'existe plus.")
                    return NotFound();
                if (model.Erreur == null)
                {
                    if (await _plainteSrv.updatePlainte(model))
                        return RedirectToAction(nameof(Dossiers));
                    model.Erreur = "La plainte n'a pas pu être modifiée.";
                }
            }

            // Les listes de choix et les pièces déjà enregistrées ne sont pas renvoyées par le formulaire : on les recharge.
            await ChargerPiecesExistantes(model);
            await RemplirListes(model);
            return View("CreatePlainte", model);
        }

        /// <summary>
        /// Fonction qui permet de charger les pièces jointes dans le viewmodel d'une plainte
        /// </summary>
        /// <param name="model">View Model de la plainte</param>
        /// <returns></returns>
        private async Task ChargerPiecesExistantes(CreatePlainteViewModel model)
        {
            var pieces = await _context.PieceJointes
                .AsNoTracking()
                .Where(pj => pj.NumPlainte == model.NumPlainte && !pj.SupprPj)
                .OrderBy(pj => pj.NumPiece)
                .Select(pj => new { pj.NumPiece, pj.LibPiece, pj.TaillePiece, pj.CodeNature, Nature = pj.CodeNatureNavigation!.LibNature, pj.PrecisionNature })
                .ToListAsync();
            model.PiecesExistantes = pieces.Select(pj => new PieceExistanteViewModel(
                pj.NumPiece,
                string.IsNullOrWhiteSpace(pj.LibPiece) ? $"Pièce {pj.NumPiece}" : pj.LibPiece,
                pj.TaillePiece,
                pj.CodeNature == "AUTR" && !string.IsNullOrWhiteSpace(pj.PrecisionNature) ? $"{pj.Nature} : {pj.PrecisionNature}" : pj.Nature)).ToList();
        }

        /// <summary>
        /// Action qui permet de télécharger la plainte avec ses pièces jointes.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="export"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [Authorize]
        [Route("{id:int}/download-pdf")]
        public async Task<IActionResult> TelechargerPlaintePdf(int id, [FromServices] IExportPlainteSrv export, CancellationToken ct)
        {
            // On récupère la plainte avec ses relations nécessaires pour générer le PDF.
            var plainte = await _context.Plaintes
                .Include(p => p.Composers.Where(c => !c.SupprDet))
                    .ThenInclude(c => c.CodeNuisanceNavigation)
                        .ThenInclude(n => n.CodeSousCatNavigation)
                .Include(p => p.PieceJointes.Where(pj => !pj.SupprPj))
                .Include(p => p.CodeRueNavigation)
                .FirstOrDefaultAsync(p => p.NumPlainte == id && !p.Suppr, ct);
            if (plainte == null)
            {
                _logger.LogWarning("Aucune plainte trouvée pour l'ID {PlainteId}.", id);
                return NotFound();
            }

            try
            {
                // Plainte convertie en PDF suivie de ses pièces jointes.
                var resultat = await export.ExporterPdfAsync(plainte, ct);

                // On log les pièces jointes qui n'ont pas pu être ajoutées au pdf final
                if (resultat.NonJointes.Count > 0)
                    _logger.LogWarning("Export PDF de la plainte {PlainteId} : {Nb} pièce(s) non intégrée(s) : {Pieces}",
                        id, resultat.NonJointes.Count, string.Join(" | ", resultat.NonJointes));

                // On retourne un File qui télécharge le pdf complet
                return File(resultat.Pdf, "application/pdf", $"plainte-{plainte.NumPlainte}.pdf");
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or IOException)
            {
                _logger.LogError(ex, "Échec de l'export PDF de la plainte {PlainteId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "L'export PDF a échoué. Veuillez réessayer ou contacter le support.");
            }
        }
    }
}
