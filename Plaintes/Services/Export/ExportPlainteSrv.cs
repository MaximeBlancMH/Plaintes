using Plaintes.Models;
using Plaintes.Services.IServices;

namespace Plaintes.Services.Export
{
    public interface IExportPlainteSrv
    {
        Task<ResultatAssemblage> ExporterPdfAsync(Plainte plainte, CancellationToken ct = default);
    }

    public class ExportPlainteSrv : IExportPlainteSrv
    {
        private readonly IPlainteSrv _plainteSrv;
        private readonly IConvertisseurPdf _convertisseur;
        private readonly IRecuperateurPieces _recuperateur;
        private readonly IAssembleurPdf _assembleur;
        private readonly ILogger<ExportPlainteSrv> _logger;

        public ExportPlainteSrv(IPlainteSrv plainteSrv, IConvertisseurPdf convertisseur, IRecuperateurPieces recuperateur,
                                IAssembleurPdf assembleur, ILogger<ExportPlainteSrv> logger)
        {
            _plainteSrv = plainteSrv;
            _convertisseur = convertisseur;
            _recuperateur = recuperateur;
            _assembleur = assembleur;
            _logger = logger;
        }

        /// <summary>
        /// Fonction globale qui permet d'exporter un objet plainte en pdf complet avec le dépôt de plainte et ses pièces jointes associées
        /// </summary>
        /// <param name="plainte">Plainte à générer</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Octets du pdf sous forme de record et la liste des pièces non jointes dans un même objet</returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        public async Task<ResultatAssemblage> ExporterPdfAsync(Plainte plainte, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plainte);

            // On récupère les valeurs à mettre dans notre fichier de plainte
            var valeurs = await _plainteSrv.getValuesForPlainteFile(plainte)
                ?? throw new InvalidOperationException("Impossible de construire les valeurs du modèle de plainte.");

            // On crée notre fichier de plainte en ajoutant les informations dans notre template et en récupèrant les octets du fichier .docx
            var docx = await _plainteSrv.createPlainteFile(valeurs);
            // On démarre la conversion de notre .docx en .pdf
            var tachePlainte = _convertisseur.ConvertirEnPdfAsync(docx, ".docx", ct);

            List<PieceExport> pieces;
            try
            {
                // On récupère chaque pièce jointe sur le disque ou dans la base
                pieces = await _recuperateur.RecupererAsync(plainte.NumPlainte, ct);
            }
            catch
            {
                // Si une erreur survient, on attend que l'erreur apparaisse plutôt que de laisser un process tourner et passer à autre chose
                await AttendreSilencieusementAsync(tachePlainte);
                throw;
            }

            // On lance en arrière plan les conversions des pièces jointes qui l'imposent 
            var tachesPieces = pieces.Select(p => ConvertirSiWordAsync(p, ct)).ToList();
            try
            {
                var pdfPlainte = await tachePlainte;
                // On attend que toutes les tâches de conversion soient terminées
                var piecesPretes = await Task.WhenAll(tachesPieces);

                // On retourne le pdf assemblé
                return await _assembleur.AssemblerAsync(pdfPlainte, piecesPretes, ct);
            }
            catch
            {
                // Si une erreur survient, on attend que l'erreur apparaisse plutôt que de laisser un process tourner et passer à autre chose
                await AttendreSilencieusementAsync(tachePlainte);
                await AttendreSilencieusementAsync(Task.WhenAll(tachesPieces));
                throw;
            }
        }

        /// <summary>
        /// Fonction qui convertit les Word en pdf
        /// </summary>
        /// <param name="piece">Record qui contient les métadonnées et le contenu du fichier d'origine</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Record qui contient les métadonnées et le contenu du nouveau fichier</returns>
        private async Task<PieceExport> ConvertirSiWordAsync(PieceExport piece, CancellationToken ct)
        {
            // Si la pièce jointe n'a pas besoin d'être convertie, on la retourne directement
            if (piece.Type != TypePieceExport.Word || !piece.EstUtilisable) return piece;
            try
            {
                // On convertit la pièce jointe en pdf
                var pdf = await _convertisseur.ConvertirEnPdfAsync(piece.Contenu!, piece.Extension, ct);
                return piece with { Type = TypePieceExport.Pdf, Extension = ".pdf", Contenu = pdf };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "La pièce {NumPiece} ({Nom}) n'a pas pu ètre convertie en PDF.", piece.NumPiece, piece.Nom);
                var raison = ex is TimeoutException ? "la conversion en PDF a dépassé le délai autorisé." : "conversion en PDF impossible.";
                return piece with { Contenu = null, Erreur = raison };
            }
        }

        /// <summary>
        /// Fonction qui permet d'attendre la tâche et ignore son exception car une autre tâche la remonte déjà
        /// </summary>
        /// <param name="tache">Tache qu'on attend</param>
        /// <returns></returns>
        private static async Task AttendreSilencieusementAsync(Task tache)
        {
            try { await tache; } catch { /* l'erreur d'origine est déjà en cours de propagation */ }
        }
    }
}
