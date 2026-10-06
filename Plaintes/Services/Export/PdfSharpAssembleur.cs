using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Plaintes.Services.Export
{
    /// <summary>Assemble le PDF d'export avec PDFsharp (reprise des PDF, images sur page A4, Word via <see cref="IConvertisseurPdf"/>).</summary>
    public class PdfSharpAssembleur : IAssembleurPdf
    {
        private const double MargeImage = 18; // marge réduite autour des photos pour les afficher le plus grand possible

        private readonly IConvertisseurPdf _convertisseur;
        private readonly ILogger<PdfSharpAssembleur> _logger;

        public PdfSharpAssembleur(IConvertisseurPdf convertisseur, ILogger<PdfSharpAssembleur> logger)
        {
            _convertisseur = convertisseur;
            _logger = logger;
        }

        public async Task<ResultatAssemblage> AssemblerAsync(byte[] pdfPlainte, IReadOnlyList<PieceExport> pieces, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(pdfPlainte);
            ArgumentNullException.ThrowIfNull(pieces);

            using var sortie = new PdfDocument();
            var nonJointes = new List<string>();

            // Le PDF de la plainte est indispensable : s'il est illisible, l'export échoue (pas de repli).
            using (var flux = new MemoryStream(pdfPlainte))
            using (var source = PdfReader.Open(flux, PdfDocumentOpenMode.Import))
                CopierPages(source, sortie);

            foreach (var piece in pieces)
            {
                ct.ThrowIfCancellationRequested();
                if (!piece.EstUtilisable)
                {
                    nonJointes.Add($"{piece.Nom} : {piece.Erreur ?? "contenu indisponible."}");
                    continue;
                }

                try
                {
                    await AjouterPieceAsync(piece, sortie, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "La pièce {NumPiece} ({Nom}) n'a pas pu être intégrée à l'export.", piece.NumPiece, piece.Nom);
                    nonJointes.Add($"{piece.Nom} : {Raison(piece, ex)}");
                }
            }

            using var resultat = new MemoryStream();
            sortie.Save(resultat, false);
            return new ResultatAssemblage(resultat.ToArray(), nonJointes);
        }

        private async Task AjouterPieceAsync(PieceExport piece, PdfDocument sortie, CancellationToken ct)
        {
            switch (piece.Type)
            {
                case TypePieceExport.Pdf:
                    AjouterPdf(piece.Contenu!, sortie);
                    break;
                case TypePieceExport.Image:
                    AjouterImage(piece.Contenu!, sortie);
                    break;
                case TypePieceExport.Word:
                    var pdf = await _convertisseur.ConvertirEnPdfAsync(piece.Contenu!, piece.Extension, ct);
                    AjouterPdf(pdf, sortie);
                    break;
                default:
                    throw new InvalidOperationException("Type de fichier non pris en charge.");
            }
        }

        // La source est ouverte en entier avant de copier : un PDF illisible ne laisse aucune page à moitié ajoutée.
        private static void AjouterPdf(byte[] contenu, PdfDocument sortie)
        {
            using var flux = new MemoryStream(contenu);
            using var source = PdfReader.Open(flux, PdfDocumentOpenMode.Import);
            if (source.PageCount == 0) throw new InvalidOperationException("Le PDF ne contient aucune page.");
            CopierPages(source, sortie);
        }

        private static void CopierPages(PdfDocument source, PdfDocument sortie)
        {
            foreach (var page in source.Pages)
                sortie.AddPage(page);
        }

        // Une page A4 par image, agrandie (ou réduite) au maximum sans déformation, centrée.
        private static void AjouterImage(byte[] contenu, PdfDocument sortie)
        {
            using var flux = new MemoryStream(contenu);
            using var image = XImage.FromStream(flux);

            var page = new PdfPage { Size = PageSize.A4 };
            //// Image paysage : page paysage, pour ne pas la réduire inutilement.
            //if (image.PixelWidth > image.PixelHeight) page.Orientation = PageOrientation.Landscape;
            sortie.AddPage(page);

            double largeurMax = page.Width.Point - 2 * MargeImage;
            double hauteurMax = page.Height.Point - 2 * MargeImage;
            double echelle = Math.Min(largeurMax / image.PointWidth, hauteurMax / image.PointHeight);
            double w = image.PointWidth * echelle;
            double h = image.PointHeight * echelle;

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(image, (page.Width.Point - w) / 2, (page.Height.Point - h) / 2, w, h);
        }

        private static string Raison(PieceExport piece, Exception ex) => ex switch
        {
            TimeoutException => "la conversion en PDF a dépassé le délai autorisé.",
            PdfReaderException => "PDF illisible, corrompu ou protégé.",
            _ when piece.Type == TypePieceExport.Word => "conversion en PDF impossible.",
            _ when piece.Type == TypePieceExport.Image => "image illisible ou format non reconnu.",
            _ => "fichier illisible ou corrompu."
        };
    }
}
