using Plaintes.Models;
using Plaintes.Services.IServices;

namespace Plaintes.Services.Export
{
    public interface IExportPlainteSrv
    {
        /// <summary>
        /// Produit le PDF d'une plainte : le Word généré depuis le modèle, converti en PDF, suivi de ses pièces jointes.
        /// La plainte doit être chargée avec ses relations (voir <see cref="IPlainteSrv.getValuesForPlainteFile"/>).
        /// </summary>
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

        public async Task<ResultatAssemblage> ExporterPdfAsync(Plainte plainte, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plainte);

            // La conversion LibreOffice est l'étape lente : on lance en même temps celle de la plainte et celles des pièces Word
            // (le convertisseur limite lui-même le nombre de conversions simultanées). Le Word de la plainte est produit d'abord,
            // car il utilise le contexte de base de données, qui n'accepte pas deux requêtes à la fois.
            var valeurs = await _plainteSrv.getValuesForPlainteFile(plainte)
                ?? throw new InvalidOperationException("Impossible de construire les valeurs du modèle de plainte.");
            var docx = await _plainteSrv.createPlainteFile(valeurs);
            var tachePlainte = _convertisseur.ConvertirEnPdfAsync(docx, ".docx", ct);

            List<PieceExport> pieces;
            try
            {
                pieces = await _recuperateur.RecupererAsync(plainte.NumPlainte, ct);
            }
            catch
            {
                await AttendreSilencieusementAsync(tachePlainte);
                throw;
            }

            var tachesPieces = pieces.Select(p => ConvertirSiWordAsync(p, ct)).ToList();
            try
            {
                var pdfPlainte = await tachePlainte;
                var piecesPretes = await Task.WhenAll(tachesPieces);
                return await _assembleur.AssemblerAsync(pdfPlainte, piecesPretes, ct);
            }
            catch
            {
                await AttendreSilencieusementAsync(tachePlainte);
                await AttendreSilencieusementAsync(Task.WhenAll(tachesPieces));
                throw;
            }
        }

        // Une pièce Word devient une pièce PDF (le contenu converti remplace l'original). Un échec de conversion ne lève pas :
        // la pièce est renvoyée avec son erreur, et l'assembleur la signale comme n'importe quelle pièce inutilisable.
        private async Task<PieceExport> ConvertirSiWordAsync(PieceExport piece, CancellationToken ct)
        {
            if (piece.Type != TypePieceExport.Word || !piece.EstUtilisable) return piece;
            try
            {
                var pdf = await _convertisseur.ConvertirEnPdfAsync(piece.Contenu!, piece.Extension, ct);
                return piece with { Type = TypePieceExport.Pdf, Extension = ".pdf", Contenu = pdf };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "La pièce {NumPiece} ({Nom}) n'a pas pu être convertie en PDF.", piece.NumPiece, piece.Nom);
                var raison = ex is TimeoutException ? "la conversion en PDF a dépassé le délai autorisé." : "conversion en PDF impossible.";
                return piece with { Contenu = null, Erreur = raison };
            }
        }

        // On n'abandonne jamais une conversion en cours : on attend sa fin (ou son échec) avant de propager l'erreur d'origine.
        private static async Task AttendreSilencieusementAsync(Task tache)
        {
            try { await tache; } catch { /* l'erreur d'origine est déjà en cours de propagation */ }
        }
    }
}
