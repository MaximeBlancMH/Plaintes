using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Diagnostics;

namespace Plaintes.Controllers
{
    [Route("")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IPlainteSrv _plainteSrv;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IPlainteSrv plainteSrv)
        {
            _logger = logger;
            _context = context;
            _plainteSrv = plainteSrv;
        }

        [Route("")]
        public IActionResult Index() => RedirectToAction("Dossiers", "Dossier");

        [Authorize(Roles = "A,S")]
        [Route("GestionTables")]
        public async Task<IActionResult> GestionTables()
        {
            var vm = new GestionTablesViewModel
            {
                Batiments = await _context.Batiments.ToListAsync(),
                BatimentRues = await _context.BatimentRues.ToListAsync(),
                Categories = await _context.Categories.ToListAsync(),
                Cites = await _context.Cites.ToListAsync(),
                CiteRues = await _context.CiteRues.ToListAsync(),
                Nuisances = await _context.Nuisances.ToListAsync(),
                PartieCommunes = await _context.PartieCommunes.ToListAsync(),
                Rues = await _context.Rues.ToListAsync(),
                SousCategories = await _context.SousCategories.ToListAsync()
            };

            return View(vm);
        }

        // Supprime l'élément choisi dans une liste de la page « Gestion des tables ». Le résultat (succès ou refus) est affiché sur la page.
        // cat : Cite, Rue, Batiment, PartieCommune, Categorie, SousCategorie ou Nuisance ; code : son identifiant.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        [Route("GestionTables/DeleteElement")]
        public async Task<IActionResult> deleteElement(string cat, string code)
        {
            string? erreur;
            try
            {
                erreur = await SupprimerElement(cat, code);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Suppression impossible ({Cat} {Code}).", cat, code);
                erreur = "Suppression impossible : cet élément est encore utilisé ailleurs dans la base.";
            }

            // La page supprime sans se recharger (requête JavaScript) : on lui répond en JSON, pour qu'elle ne remette pas toutes ses listes à zéro.
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = erreur == null, message = erreur ?? "Élément supprimé." });

            if (erreur == null) TempData["Succes"] = "Élément supprimé.";
            else TempData["Erreur"] = erreur;
            return RedirectToAction(nameof(GestionTables));
        }

        // Ajoute un élément. Les champs du formulaire portent le nom des colonnes (LibCite, Secteur, NumCite...) ; voir la page.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "A,S")]
        [Route("GestionTables/AddElement")]
        public async Task<IActionResult> addElement(string cat, IFormCollection form)
        {
            string? erreur;
            var cree = new List<object>();      // l'élément créé, pour que la page mette ses listes à jour sans se recharger
            try
            {
                erreur = await AjouterElement(cat, form, cree);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Ajout impossible ({Cat}).", cat);
                erreur = "Ajout impossible : la base a refusé cet élément.";
            }

            // La page ajoute sans se recharger (requête JavaScript) : on lui répond en JSON, avec l'élément créé.
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = erreur == null, message = erreur ?? "Élément ajouté.", element = cree.FirstOrDefault() });

            if (erreur == null) TempData["Succes"] = "Élément ajouté.";
            else TempData["Erreur"] = erreur;
            return RedirectToAction(nameof(GestionTables));
        }

        // ---------- Suppression ----------
        // Cité, rue et bâtiment n'ont pas de colonne « supprimé » : ils sont vraiment supprimés, sauf s'ils sont utilisés.
        // Partie commune, catégorie, sous-catégorie et nuisance sont seulement marquées supprimées (les plaintes existantes les gardent).
        // Renvoie null si tout s'est bien passé, sinon le message à afficher.
        private async Task<string?> SupprimerElement(string cat, string code)
        {
            switch (cat)
            {
                case "Cite":
                {
                    if (!int.TryParse(code, out var num)) return "Identifiant invalide.";
                    var cite = await _context.Cites.FindAsync(num);
                    if (cite == null) return "Élément introuvable.";
                    if (await _context.Plaintes.AnyAsync(p => p.NumCite == num)) return "Cette cité est utilisée par des plaintes : elle ne peut pas être supprimée.";
                    if (await _context.Batiments.AnyAsync(b => b.NumCite == num)) return "Supprimez d'abord les bâtiments de cette cité.";
                    _context.CiteRues.RemoveRange(_context.CiteRues.Where(l => l.NumCite == num));
                    _context.Cites.Remove(cite);
                    break;
                }
                case "Rue":
                {
                    if (!int.TryParse(code, out var num)) return "Identifiant invalide.";
                    var rue = await _context.Rues.FindAsync(num);
                    if (rue == null) return "Élément introuvable.";
                    if (await _context.Plaintes.AnyAsync(p => p.CodeRue == num)) return "Cette rue est utilisée par des plaintes : elle ne peut pas être supprimée.";
                    if (await _context.BatimentRues.AnyAsync(b => b.CodeRue == num)) return "Supprimez d'abord les bâtiments de cette rue.";
                    _context.CiteRues.RemoveRange(_context.CiteRues.Where(l => l.CodeRue == num));
                    _context.Rues.Remove(rue);
                    break;
                }
                case "Batiment":
                {
                    if (!int.TryParse(code, out var num)) return "Identifiant invalide.";
                    var batiment = await _context.Batiments.FindAsync(num);
                    if (batiment == null) return "Élément introuvable.";
                    _context.BatimentRues.RemoveRange(_context.BatimentRues.Where(b => b.IdBat == num));
                    _context.Batiments.Remove(batiment);
                    break;
                }
                case "PartieCommune":
                {
                    var partie = await _context.PartieCommunes.FindAsync(code);
                    if (partie == null || partie.SupprPc) return "Élément introuvable.";
                    partie.SupprPc = true;
                    break;
                }
                case "Categorie":
                {
                    var categorie = await _context.Categories.FindAsync(code);
                    if (categorie == null || categorie.SupprCat) return "Élément introuvable.";
                    if (await _context.SousCategories.AnyAsync(s => s.CodeCat == code && !s.SupprSousCat)) return "Supprimez d'abord les sous-catégories de cette catégorie.";
                    categorie.SupprCat = true;
                    break;
                }
                case "SousCategorie":
                {
                    var sousCategorie = await _context.SousCategories.FindAsync(code);
                    if (sousCategorie == null || sousCategorie.SupprSousCat) return "Élément introuvable.";
                    if (await _context.Nuisances.AnyAsync(n => n.CodeSousCat == code && !n.SupprNuisance)) return "Supprimez d'abord les nuisances de cette sous-catégorie.";
                    sousCategorie.SupprSousCat = true;
                    break;
                }
                case "Nuisance":
                {
                    var nuisance = await _context.Nuisances.FindAsync(code);
                    if (nuisance == null || nuisance.SupprNuisance) return "Élément introuvable.";
                    nuisance.SupprNuisance = true;
                    break;
                }
                default:
                    return "Type d'élément inconnu.";
            }

            await _context.SaveChangesAsync();
            return null;
        }

        // ---------- Ajout ----------
        // Contrôles volontairement simples : champs obligatoires, longueurs de la base, code pas déjà pris, parent existant.
        // Renvoie null si tout s'est bien passé, sinon le message à afficher.
        private async Task<string?> AjouterElement(string cat, IFormCollection form, List<object> cree)
        {
            Func<object>? element = null;   // décrit l'élément créé
            string F(string champ) => form[champ].ToString().Trim();
            string? Verifier(params (string Nom, string Valeur, int Max)[] champs)
            {
                foreach (var (nom, valeur, max) in champs)
                {
                    if (valeur.Length == 0) return $"« {nom} » est obligatoire.";
                    if (valeur.Length > max) return $"« {nom} » ne peut pas dépasser {max} caractères.";
                }
                return null;
            }

            switch (cat)
            {
                case "Cite":
                {
                    var erreur = Verifier(("Libellé", F("LibCite"), 100), ("Secteur", F("Secteur"), 10));
                    if (erreur != null) return erreur;
                    var num = (await _context.Cites.MaxAsync(c => (int?)c.NumCite) ?? 0) + 1;   // NUMCITE n'est pas auto-incrémenté
                    var cite = new Cite { NumCite = num, LibCite = F("LibCite"), Secteur = F("Secteur").ToUpperInvariant() };
                    _context.Cites.Add(cite);
                    element = () => new { id = cite.NumCite, lib = cite.LibCite, secteur = cite.Secteur };
                    break;
                }
                case "Rue":
                {
                    var erreur = Verifier(("Libellé", F("LibRue"), 150), ("Code postal", F("Cp"), 50), ("Ville", F("Ville"), 50));
                    if (erreur != null) return erreur;
                    if (!int.TryParse(F("NumCite"), out var numCite) || !await _context.Cites.AnyAsync(c => c.NumCite == numCite)) return "La cité choisie n'existe pas.";
                    var code = (await _context.Rues.MaxAsync(r => (int?)r.CodeRue) ?? 0) + 1;   // CODERUE n'est pas auto-incrémenté
                    var rue = new Rue { CodeRue = code, LibRue = F("LibRue"), Cp = F("Cp"), Ville = F("Ville") };
                    _context.Rues.Add(rue);
                    element = () => new { id = rue.CodeRue, lib = rue.LibRue, cp = rue.Cp, ville = rue.Ville, cite = numCite };
                    _context.CiteRues.Add(new CiteRue { NumCite = numCite, CodeRue = code });   // la rue est rattachée à la cité choisie
                    break;
                }
                case "Batiment":
                {
                    var erreur = Verifier(("N° de bâtiment", F("NumBat"), 11), ("N° dans la rue", F("NumRue"), 50));
                    if (erreur != null) return erreur;
                    if (F("LibBat").Length > 50) return "« Libellé » ne peut pas dépasser 50 caractères.";
                    if (F("CodeTranche").Length > 5) return "« Tranche » ne peut pas dépasser 5 caractères.";
                    if (!int.TryParse(F("NumCite"), out var numCite) || !await _context.Cites.AnyAsync(c => c.NumCite == numCite)) return "La cité choisie n'existe pas.";
                    if (!int.TryParse(F("CodeRue"), out var codeRue) || !await _context.Rues.AnyAsync(r => r.CodeRue == codeRue)) return "La rue choisie n'existe pas.";
                    // Le bâtiment et son adresse sont enregistrés ensemble : IDBAT est généré par la base.
                    var batiment = new Batiment
                    {
                        NumCite = numCite,
                        NumBat = F("NumBat"),
                        LibBat = F("LibBat").Length == 0 ? null : F("LibBat"),
                        CodeTranche = F("CodeTranche").Length == 0 ? null : F("CodeTranche"),
                        BatimentRues = { new BatimentRue { CodeRue = codeRue, NumRue = F("NumRue") } }
                    };
                    _context.Batiments.Add(batiment);
                    element = () => new { id = batiment.IdBat, cite = batiment.NumCite, num = batiment.NumBat, lib = batiment.LibBat, tranche = batiment.CodeTranche, rue = codeRue, numRue = F("NumRue") };
                    break;
                }
                case "PartieCommune":
                {
                    var code = F("CodePartieCommune").ToUpperInvariant();
                    var erreur = Verifier(("Code", code, 4), ("Libellé", F("LibPartieCommune"), 50));
                    if (erreur != null) return erreur;
                    if (await _context.PartieCommunes.AnyAsync(p => p.CodePartieCommune == code)) return "Ce code existe déjà.";
                    var partie = new PartieCommune { CodePartieCommune = code, LibPartieCommune = F("LibPartieCommune") };
                    _context.PartieCommunes.Add(partie);
                    element = () => new { code = partie.CodePartieCommune, lib = partie.LibPartieCommune };
                    break;
                }
                case "Categorie":
                {
                    var code = F("CodeCat").ToUpperInvariant();
                    var erreur = Verifier(("Code", code, 4), ("Libellé", F("LibCat"), 100));
                    if (erreur != null) return erreur;
                    if (await _context.Categories.AnyAsync(c => c.CodeCat == code)) return "Ce code existe déjà.";
                    var categorie = new Categorie { CodeCat = code, LibCat = F("LibCat") };
                    _context.Categories.Add(categorie);
                    element = () => new { code = categorie.CodeCat, lib = categorie.LibCat };
                    break;
                }
                case "SousCategorie":
                {
                    var code = F("CodeSousCat").ToUpperInvariant();
                    var erreur = Verifier(("Code", code, 5), ("Libellé", F("LibSousCat"), 150));
                    if (erreur != null) return erreur;
                    var codeCat = F("CodeCat");
                    if (!await _context.Categories.AnyAsync(c => c.CodeCat == codeCat && !c.SupprCat)) return "La catégorie choisie n'existe pas.";
                    if (await _context.SousCategories.AnyAsync(s => s.CodeSousCat == code)) return "Ce code existe déjà.";
                    var sousCategorie = new SousCategorie { CodeSousCat = code, CodeCat = codeCat, LibSousCat = F("LibSousCat") };
                    _context.SousCategories.Add(sousCategorie);
                    element = () => new { code = sousCategorie.CodeSousCat, cat = sousCategorie.CodeCat, lib = sousCategorie.LibSousCat };
                    break;
                }
                case "Nuisance":
                {
                    var code = F("CodeNuisance").ToUpperInvariant();
                    var erreur = Verifier(("Code", code, 5), ("Libellé", F("LibNuisance"), 100));
                    if (erreur != null) return erreur;
                    var codeSousCat = F("CodeSousCat");
                    if (!await _context.SousCategories.AnyAsync(s => s.CodeSousCat == codeSousCat && !s.SupprSousCat)) return "La sous-catégorie choisie n'existe pas.";
                    if (await _context.Nuisances.AnyAsync(n => n.CodeNuisance == code)) return "Ce code existe déjà.";
                    var nuisance = new Nuisance { CodeNuisance = code, CodeSousCat = codeSousCat, LibNuisance = F("LibNuisance") };
                    _context.Nuisances.Add(nuisance);
                    element = () => new { code = nuisance.CodeNuisance, sousCat = nuisance.CodeSousCat, lib = nuisance.LibNuisance };
                    break;
                }
                default:
                    return "Type d'élément inconnu.";
            }

            await _context.SaveChangesAsync();
            if (element != null) cree.Add(element());
            return null;
        }

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}
    }
}
