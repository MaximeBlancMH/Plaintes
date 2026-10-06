using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Services.Export;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Plaintes.Tests
{
    public class AssembleurPdfTests
    {
        // PNG 1x1 pixel valide.
        private static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        private sealed class FauxConvertisseur : IConvertisseurPdf
        {
            public byte[]? PdfRenvoye { get; set; }
            public Exception? Erreur { get; set; }
            public List<string> Extensions { get; } = new();

            public Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default)
            {
                Extensions.Add(extension);
                if (Erreur != null) throw Erreur;
                return Task.FromResult(PdfRenvoye ?? throw new InvalidOperationException("Pas de PDF prévu."));
            }
        }

        private static byte[] Pdf(int pages, string? motDePasse = null)
        {
            using var doc = new PdfDocument();
            for (int i = 0; i < pages; i++) doc.AddPage();
            if (motDePasse != null)
            {
                doc.SecuritySettings.UserPassword = motDePasse;
                doc.SecuritySettings.OwnerPassword = motDePasse;
            }
            using var ms = new MemoryStream();
            doc.Save(ms, false);
            return ms.ToArray();
        }

        private static int NbPages(byte[] pdf)
        {
            using var ms = new MemoryStream(pdf);
            using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            return doc.PageCount;
        }

        private static PieceExport Piece(int num, string nom, TypePieceExport type, byte[]? contenu, string? erreur = null) =>
            new(num, nom, Path.GetExtension(nom).ToLowerInvariant(), type, "Facture", contenu, erreur);

        private static (PdfSharpAssembleur, FauxConvertisseur) Creer()
        {
            var conv = new FauxConvertisseur();
            return (new PdfSharpAssembleur(conv, NullLogger<PdfSharpAssembleur>.Instance), conv);
        }

        [Fact]
        public async Task SansPiece_RenvoieLePdfDeLaPlainte()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(2), new List<PieceExport>());
            Assert.Equal(2, NbPages(r.Pdf));
            Assert.Empty(r.NonJointes);
        }

        [Fact]
        public async Task PdfJoint_EstAjouteApresLaPlainte()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(2), new[] { Piece(1, "a.pdf", TypePieceExport.Pdf, Pdf(3)) });
            Assert.Equal(5, NbPages(r.Pdf));
            Assert.Empty(r.NonJointes);
        }

        [Fact]
        public async Task Image_DonneUnePage()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "photo.png", TypePieceExport.Image, Png) });
            Assert.Equal(2, NbPages(r.Pdf));
            Assert.Empty(r.NonJointes);
        }

        [Fact]
        public async Task Word_EstConvertiPuisAjoute_ExtensionTransmise()
        {
            var (a, conv) = Creer();
            conv.PdfRenvoye = Pdf(2);
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "courrier.docx", TypePieceExport.Word, new byte[] { 1, 2, 3 }) });
            Assert.Equal(3, NbPages(r.Pdf));
            Assert.Equal(new[] { ".docx" }, conv.Extensions);
        }

        [Fact]
        public async Task OrdreDesPieces_EstRespecte()
        {
            var (a, conv) = Creer();
            conv.PdfRenvoye = Pdf(4);
            // plainte 1 page + pdf 2 pages + word (4 pages) + image 1 page = 8 pages
            var r = await a.AssemblerAsync(Pdf(1), new[]
            {
                Piece(1, "a.pdf", TypePieceExport.Pdf, Pdf(2)),
                Piece(2, "b.docx", TypePieceExport.Word, new byte[] { 1 }),
                Piece(3, "c.png", TypePieceExport.Image, Png),
            });
            Assert.Equal(8, NbPages(r.Pdf));
        }

        [Fact]
        public async Task PieceEnErreur_EstListee_EtAjouteUnePage()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[]
            {
                Piece(1, "ok.pdf", TypePieceExport.Pdf, Pdf(1)),
                Piece(2, "perdu.pdf", TypePieceExport.Pdf, null, "Le fichier de cette pièce est introuvable."),
            });
            Assert.Equal(2, NbPages(r.Pdf));        // plainte + ok.pdf, sans page supplémentaire
            var ligne = Assert.Single(r.NonJointes);
            Assert.Contains("perdu.pdf", ligne);
            Assert.Contains("introuvable", ligne);
        }

        [Fact]
        public async Task PdfCorrompu_EstListe_SansBloquerLesAutres()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[]
            {
                Piece(1, "casse.pdf", TypePieceExport.Pdf, new byte[] { 1, 2, 3, 4 }),
                Piece(2, "ok.pdf", TypePieceExport.Pdf, Pdf(2)),
            });
            Assert.Single(r.NonJointes);
            Assert.Contains("casse.pdf", r.NonJointes[0]);
            Assert.Equal(1 + 2, NbPages(r.Pdf));
        }

        [Fact]
        public async Task PdfProtegeParMotDePasse_EstListe()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "secret.pdf", TypePieceExport.Pdf, Pdf(1, "mdp")) });
            Assert.Single(r.NonJointes);
            Assert.Equal(1, NbPages(r.Pdf));        // plainte seule
        }

        [Fact]
        public async Task ImageIllisible_EstListee()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "x.png", TypePieceExport.Image, new byte[] { 9, 9, 9 }) });
            Assert.Single(r.NonJointes);
        }

        [Fact]
        public async Task ConversionEnEchec_EstListee_ConversionEnDelaiAussi()
        {
            var (a, conv) = Creer();
            conv.Erreur = new TimeoutException("trop long");
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "gros.docx", TypePieceExport.Word, new byte[] { 1 }) });
            Assert.Contains("délai", Assert.Single(r.NonJointes));

            conv.Erreur = new InvalidOperationException("LibreOffice a échoué");
            r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "mauvais.docx", TypePieceExport.Word, new byte[] { 1 }) });
            Assert.Contains("conversion", Assert.Single(r.NonJointes));
        }

        [Fact]
        public async Task TypeNonPris_EstListe()
        {
            var (a, _) = Creer();
            var r = await a.AssemblerAsync(Pdf(1), new[] { Piece(1, "x.zip", TypePieceExport.NonPris, null, "Type de fichier non pris en charge (.zip).") });
            Assert.Contains("x.zip", Assert.Single(r.NonJointes));
        }

        [Fact]
        public async Task PdfPlainteIllisible_Leve()
        {
            var (a, _) = Creer();
            await Assert.ThrowsAnyAsync<Exception>(() => a.AssemblerAsync(new byte[] { 1, 2, 3 }, new List<PieceExport>()));
        }

        [Fact]
        public async Task Annulation_EstPropagee()
        {
            var (a, _) = Creer();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                a.AssemblerAsync(Pdf(1), new[] { Piece(1, "a.pdf", TypePieceExport.Pdf, Pdf(1)) }, cts.Token));
        }
    }

    public class ExportPlainteSrvTests
    {
        [SofficeFact]
        public async Task BoutEnBout_AvecLibreOffice_ProduitUnPdfAvecLesPieces()
        {
            // Vérifie que le chemin complet (docx de la plainte -> PDF -> assemblage) fonctionne avec un vrai LibreOffice.
            var conv = new LibreOfficeConvertisseurPdf(
                Microsoft.Extensions.Options.Options.Create(new ExportSettings { SofficePath = ConvertisseurPdfTests.SofficeChemin ?? "soffice" }),
                NullLogger<LibreOfficeConvertisseurPdf>.Instance);
            var assembleur = new PdfSharpAssembleur(conv, NullLogger<PdfSharpAssembleur>.Instance);

            using var ms = new MemoryStream();
            using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Annexe")))));
            }
            var docx = ms.ToArray();

            var pdfPlainte = await conv.ConvertirEnPdfAsync(docx, ".docx");
            var r = await assembleur.AssemblerAsync(pdfPlainte, new[]
            {
                new PieceExport(1, "annexe.docx", ".docx", TypePieceExport.Word, "Autre", docx, null),
            });
            Assert.Empty(r.NonJointes);
            using var res = new MemoryStream(r.Pdf);
            using var pdf = PdfReader.Open(res, PdfDocumentOpenMode.Import);
            Assert.Equal(2, pdf.PageCount);
        }
    }
}
