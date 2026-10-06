using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.Stockage;

namespace Plaintes.Services.Export
{
    public class RecuperateurPieces : IRecuperateurPieces
    {
        private const string CodeAutre = "AUTR";

        // Extension -> traitement. Même liste que Storage:Extensions, moins les formats que l'export ne sait pas intégrer.
        private static readonly Dictionary<string, TypePieceExport> TypesParExtension = new()
        {
            [".pdf"] = TypePieceExport.Pdf,
            [".jpg"] = TypePieceExport.Image,
            [".jpeg"] = TypePieceExport.Image,
            [".png"] = TypePieceExport.Image,
            [".doc"] = TypePieceExport.Word,
            [".docx"] = TypePieceExport.Word,
        };

        // Anciennes pièces sans extension dans leur nom : on se rabat sur le type MIME enregistré (piece_jointe.TYPEPIECE).
        private static readonly Dictionary<string, string> ExtensionParMime = new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["application/msword"] = ".doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        };

        private readonly AppDbContext _context;
        private readonly IPieceStorage _storage;
        private readonly ILogger<RecuperateurPieces> _logger;

        public RecuperateurPieces(AppDbContext context, IPieceStorage storage, ILogger<RecuperateurPieces> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        public async Task<List<PieceExport>> RecupererAsync(int numPlainte, CancellationToken ct = default)
        {
            var pieces = await _context.PieceJointes
                .AsNoTracking()
                .Include(pj => pj.CodeNatureNavigation)
                .Where(pj => pj.NumPlainte == numPlainte && !pj.SupprPj)
                .OrderBy(pj => pj.NumPiece)
                .ToListAsync(ct);

            var resultat = new List<PieceExport>(pieces.Count);
            foreach (var pj in pieces)
                resultat.Add(await ConstruireAsync(pj, ct));
            return resultat;
        }

        private async Task<PieceExport> ConstruireAsync(PieceJointe pj, CancellationToken ct)
        {
            var nom = NomSansDossier(pj.LibPiece);
            if (nom == "") nom = $"piece-{pj.NumPiece}";

            var extension = Path.GetExtension(nom).ToLowerInvariant();
            if (!TypesParExtension.ContainsKey(extension) && pj.TypePiece is not null &&
                ExtensionParMime.TryGetValue(pj.TypePiece.Split(';')[0].Trim(), out var depuisMime))
                extension = depuisMime;

            var nature = LibelleNature(pj);

            if (!TypesParExtension.TryGetValue(extension, out var type))
                return new PieceExport(pj.NumPiece, nom, extension, TypePieceExport.NonPris, nature, null,
                    $"Type de fichier non pris en charge ({(extension == "" ? "sans extension" : extension)}).");

            try
            {
                var contenu = await LireContenuAsync(pj, ct);
                if (contenu is null)
                    return new PieceExport(pj.NumPiece, nom, extension, type, nature, null, "Le fichier de cette pièce est introuvable.");
                if (contenu.Length == 0)
                    return new PieceExport(pj.NumPiece, nom, extension, type, nature, null, "Le fichier de cette pièce est vide.");
                return new PieceExport(pj.NumPiece, nom, extension, type, nature, contenu, null);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Impossible de lire la pièce jointe {NumPiece}.", pj.NumPiece);
                return new PieceExport(pj.NumPiece, nom, extension, type, nature, null, "Le fichier de cette pièce n'a pas pu être lu.");
            }
        }

        // Nouvelles pièces : fichier sur disque (CheminPiece). Anciennes : contenu en base (Fichier). Null = introuvable.
        private async Task<byte[]?> LireContenuAsync(PieceJointe pj, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(pj.CheminPiece))
            {
                if (!_storage.Exists(pj.CheminPiece))
                {
                    _logger.LogWarning("Le fichier de la pièce jointe {NumPiece} est absent du stockage ({Chemin}).", pj.NumPiece, pj.CheminPiece);
                    return null;
                }
                await using var flux = _storage.Open(pj.CheminPiece);
                using var ms = new MemoryStream();
                await flux.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
            return pj.Fichier;
        }

        private static string LibelleNature(PieceJointe pj)
        {
            var libelle = pj.CodeNatureNavigation?.LibNature?.Trim() ?? "";
            if (pj.CodeNature == CodeAutre && !string.IsNullOrWhiteSpace(pj.PrecisionNature))
                return $"{libelle} : {pj.PrecisionNature.Trim()}".TrimStart(' ', ':');
            return libelle;
        }

        // Anciennes données : le nom peut contenir un dossier ("C:\x\a.pdf") ; on garde seulement le nom, quel que soit le séparateur.
        private static string NomSansDossier(string? nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return "";
            return nom[(nom.LastIndexOfAny(new[] { '/', '\\' }) + 1)..].Trim();
        }
    }
}
