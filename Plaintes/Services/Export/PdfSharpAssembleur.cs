using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Plaintes.Services.Export
{
    /// <summary>Assemble le PDF d'export avec PDFsharp (reprise des PDF, images sur page A4, Word via <see cref="IConvertisseurPdf"/>).</summary>
    public class PdfSharpAssembleur : IAssembleurPdf
    {
        private const double MargeImage = 18; // On réduit la marge pour que la photo apparaisse la plus grande possible

        private readonly IConvertisseurPdf _convertisseur;
        private readonly ILogger<PdfSharpAssembleur> _logger;

        public PdfSharpAssembleur(IConvertisseurPdf convertisseur, ILogger<PdfSharpAssembleur> logger)
        {
            _convertisseur = convertisseur;
            _logger = logger;
        }

        /// <summary>
        /// Fonction qui assemble le pdf de la plainte avec toutes ses pièces jointes en pdf
        /// </summary>
        /// <param name="pdfPlainte">Octets du contenu de la plainte</param>
        /// <param name="pieces">Liste d'objet contenant pour chaque pièce jointe, ses métadonnées et son contenu</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Les octets du pdf final dans l'objet ResultatAssemblage qui contient aussi la liste 
        /// des pièces jointes qui n'ont pas été ajoutées au pdf final</returns>
        public async Task<ResultatAssemblage> AssemblerAsync(byte[] pdfPlainte, IReadOnlyList<PieceExport> pieces, CancellationToken ct = default)
        {
            // On renvoie directement une erreur si le contenu des pdf est null
            ArgumentNullException.ThrowIfNull(pdfPlainte);
            ArgumentNullException.ThrowIfNull(pieces);

            using var sortie = new PdfDocument();
            var nonJointes = new List<string>();

            // On récupère le flux du pdf de la plainte. Ensuite, on ouvre ce pdf et on ajoute les pages à notre PdfDocument  
            using (var flux = new MemoryStream(pdfPlainte))
            using (var source = PdfReader.Open(flux, PdfDocumentOpenMode.Import))
                CopierPages(source, sortie);

            // On parcourt chaque pièce jointe pour l'ajouter à notre PdfDocument
            foreach (var piece in pieces)
            {
                // On annule si elle a été demandée
                ct.ThrowIfCancellationRequested();
                if (!piece.EstUtilisable)
                {
                    // Si la pièce ne peut toujours pas être ajoutée au pdf pour une raison ou une autre, on l'ajoute dans la liste pour les logs
                    nonJointes.Add($"{piece.Nom} : {piece.Erreur ?? "contenu indisponible."}");
                    continue;
                }

                try
                {
                    // On ajoute la piece jointe à notre PdfDocument de sortie
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

            // On sauvegarde les octets dans une variable "resultat" puis on peut retourner le pdf et la liste des pièces non jointes
            using var resultat = new MemoryStream();
            sortie.Save(resultat, false);
            return new ResultatAssemblage(resultat.ToArray(), nonJointes);
        }

        /// <summary>
        /// On redirige vers la fonction d'ajout adaptée selon le type de la pièce jointe
        /// </summary>
        /// <param name="piece">Pièce jointe à ajouter au document final</param>
        /// <param name="sortie">Document final</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
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

        /// <summary>
        /// Fonction qui permet d'ajouter un pdf au document de sortie
        /// </summary>
        /// <param name="contenu">Contenu à ajouter (en octets)</param>
        /// <param name="sortie">Document de sortie</param>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        private static void AjouterPdf(byte[] contenu, PdfDocument sortie)
        {
            using var flux = new MemoryStream(contenu);
            using var source = PdfReader.Open(flux, PdfDocumentOpenMode.Import);
            if (source.PageCount == 0) throw new InvalidOperationException("Le PDF ne contient aucune page.");
            CopierPages(source, sortie);
        }

        /// <summary>
        /// Fonction qui copie chaque page du pdf source dans le pdf de sortie
        /// </summary>
        /// <param name="source">Document source, on récupère les pages depuis ce dernier</param>
        /// <param name="sortie">Document de sortie, on copie les pages ici</param>
        private static void CopierPages(PdfDocument source, PdfDocument sortie)
        {
            foreach (var page in source.Pages)
                sortie.AddPage(page);
        }

        /// <summary>
        /// Fonction qui permet de copier une image dans le document de sortie. On ajoute une nouvelle 
        /// page pdf au fichier de sortie puis on y dessine l'image
        /// </summary>
        /// <param name="contenu">Octets de l'image</param>
        /// <param name="sortie">Document de sortie</param>
        private static void AjouterImage(byte[] contenu, PdfDocument sortie)
        {
            // On transforme les octets en flux pour pouvoir récupérer l'image en elle même
            using var flux = new MemoryStream(contenu);
            using var image = XImage.FromStream(flux);

            // On crée une page PDF, c'est elle qui accueillera l'image
            var page = new PdfPage { Size = PageSize.A4 };
            sortie.AddPage(page);

            // On calcule les dimensions de la page pdf puis les dimensions de l'image, on agrandit cette dernière au maximum
            double largeurMax = page.Width.Point - 2 * MargeImage;
            double hauteurMax = page.Height.Point - 2 * MargeImage;
            double echelle = Math.Min(largeurMax / image.PointWidth, hauteurMax / image.PointHeight);
            double w = image.PointWidth * echelle;
            double h = image.PointHeight * echelle;

            // On dessine l'image sur la page pdf
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(image, (page.Width.Point - w) / 2, (page.Height.Point - h) / 2, w, h);
        }

        /// <summary>
        /// Fonction qui retourne le message d'erreur correspondant à l'exception renvoyée
        /// </summary>
        /// <param name="piece">Piece jointe en erreur</param>
        /// <param name="ex">Exception renvoyée</param>
        /// <returns>Message d'erreur</returns>
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
