using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Diagnostics;

namespace Plaintes.Controllers
{
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

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Authorize]
        [Route("Dossiers")]
        public async Task<IActionResult> Dossiers(PlainteFiltres filtres, int page = 1, int pageSize = 10)
        {
            if (pageSize is not (10 or 25 or 50)) pageSize = 10;

            // On récupère les plaintes selon les filtres
            PagedResult<Plainte> result = await _plainteSrv.getPlaintesWithFilters(filtres, page, pageSize);

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

        [Authorize]
        [Route("PiecesJointes/{id:int}")]
        public async Task<IActionResult> PieceJointeFichier(int id)
        {
            // On récupère la pièce jointe dans la base de données.
            var pj = await _context.PieceJointes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NumPiece == id && !x.SupprPj);
            if (pj?.Fichier == null) return NotFound();

            // On récupère le nom de la pièce jointe ou on génère un nom avec son numéro s'il n'existe pas.
            var nom = string.IsNullOrWhiteSpace(pj.LibPiece) ? $"piece-{pj.NumPiece}" : pj.LibPiece;
            // On récupère le type du fichier afin de savoir comment l'envoyer au navigateur. S'il n'existe pas, on envoie un type générique.
            var type = Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(pj.TypePiece, out var parsed)
                ? parsed.MediaType.Value!
                : "application/octet-stream";

            // Seuls les PDF et les images s'ouvrent dans le navigateur ; le reste est téléchargé.
            var inline = type == "application/pdf" || type.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            Response.Headers.ContentDisposition =
                $"{(inline ? "inline" : "attachment")}; filename*=UTF-8''{Uri.EscapeDataString(nom)}";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(pj.Fichier, type);
        }
    }
}
