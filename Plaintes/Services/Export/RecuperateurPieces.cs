using Humanizer;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.Stockage;

namespace Plaintes.Services.Export
{
    public class RecuperateurPieces : IRecuperateurPieces
    {
        /// <summary>
        /// Dictionnaires des types pour chaque extension
        /// </summary>
        private static readonly Dictionary<string, TypePieceExport> TypesParExtension = new()
        {
            [".pdf"] = TypePieceExport.Pdf,
            [".jpg"] = TypePieceExport.Image,
            [".jpeg"] = TypePieceExport.Image,
            [".png"] = TypePieceExport.Image,
            [".doc"] = TypePieceExport.Word,
            [".docx"] = TypePieceExport.Word,
        };

        /// <summary>
        /// Dictionnaire pour récupérer l'extension des pièces jointes dont l'extension n'est pas connue
        /// </summary>
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

        /// <summary>
        /// Récupère les métadonnées des pièces jointes d'une plainte en base de données ainsi que leur contenu sur le disque.
        /// </summary>
        /// <param name="numPlainte">Numéro de la plainte</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Liste d'objets contenant les informations des pièces jointes de la plainte associée (métadonnées + contenu)</returns>
        public async Task<List<PieceExport>> RecupererAsync(int numPlainte, CancellationToken ct = default)
        {
            // On récupère les pièce jointes pour la plainte retrouvée avec NumPlainte
            var pieces = await _context.PieceJointes
                .AsNoTracking()
                .Where(pj => pj.NumPlainte == numPlainte && !pj.SupprPj)
                .OrderBy(pj => pj.NumPiece)
                .ToListAsync(ct);

            // On initialise notre liste puis on lui ajoute chaque pièce jointe
            var resultat = new List<PieceExport>(pieces.Count);
            foreach (var pj in pieces)
                resultat.Add(await ConstruireAsync(pj, ct));
            return resultat;
        }

        /// <summary>
        /// Fonction qui remplit l'objet PieceExport qui contient les métadonnées ainsi que le contenu de la pièce jointe
        /// </summary>
        /// <param name="pj">Piece jointe à "transformer"</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Piece Export</returns>
        private async Task<PieceExport> ConstruireAsync(PieceJointe pj, CancellationToken ct)
        {
            // On récupère le nom de la pièce jointe
            var nom = NomSansDossier(pj.LibPiece);
            if (nom == "") nom = $"piece-{pj.NumPiece}";

            // On récupère son extension
            var extension = Path.GetExtension(nom).ToLowerInvariant();
            if (!TypesParExtension.ContainsKey(extension) && pj.TypePiece is not null &&
                ExtensionParMime.TryGetValue(pj.TypePiece.Split(';')[0].Trim(), out var depuisMime))
                extension = depuisMime;

            if (!TypesParExtension.TryGetValue(extension, out var type))
                return new PieceExport(pj.NumPiece, nom, extension, TypePieceExport.NonPris, null,
                    $"Type de fichier non pris en charge ({(extension == "" ? "sans extension" : extension)}).");

            try
            {
                // On récupère enfin le contenu du fichier. S'il est null ou vide, on ajoute une erreur dans PieceExport
                // sinon on ajoute le contenu dans PieceExport et on met erreur à null
                var contenu = await LireContenuAsync(pj, ct);
                if (contenu is null)
                    return new PieceExport(pj.NumPiece, nom, extension, type, null, "Le fichier de cette pièce est introuvable.");
                if (contenu.Length == 0)
                    return new PieceExport(pj.NumPiece, nom, extension, type, null, "Le fichier de cette pièce est vide.");
                return new PieceExport(pj.NumPiece, nom, extension, type, contenu, null);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Impossible de lire la pièce jointe {NumPiece}.", pj.NumPiece);
                return new PieceExport(pj.NumPiece, nom, extension, type, null, "Le fichier de cette pièce n'a pas pu être lu.");
            }
        }

        /// <summary>
        /// Fonction qui permet de lire le contenu d'un fichier stocké sur le disque ou en base de données
        /// </summary>
        /// <param name="pj">Pièce jointe à lire</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Octets de la pièce jointe</returns>
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

        /// <summary>
        /// Fonction qui extrait seulement le nom d'un fichier si jamais un fichier était stocké avec son chemin complet
        /// </summary>
        /// <param name="nom">Nom actuel</param>
        /// <returns>Nom du fichier seul (exemple : "C:/users/mh/file.pdf" devient "file.pdf")</returns>
        private static string NomSansDossier(string? nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return "";
            return nom[(nom.LastIndexOfAny(new[] { '/', '\\' }) + 1)..].Trim();
        }
    }
}
