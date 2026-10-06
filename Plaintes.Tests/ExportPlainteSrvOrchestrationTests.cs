using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Models;
using Plaintes.Services.Export;
using Plaintes.Services.IServices;

namespace Plaintes.Tests
{
    public class ExportPlainteSrvOrchestrationTests
    {
        private sealed class FauxPlainteSrv : IPlainteSrv
        {
            public Task<Dictionary<string, string>> getValuesForPlainteFile(Plainte plainte) => Task.FromResult(new Dictionary<string, string>());
            public Task<byte[]> createPlainteFile(Dictionary<string, string> valeurs) => Task.FromResult(new byte[] { 1 });
            public Task<List<Plainte>> getPlaintes() => throw new NotImplementedException();
            public Task<PagedResult<Plainte>> getPlaintesForPage(int pageNumber, int pageSize) => throw new NotImplementedException();
            public Task<PagedResult<Plainte>> getPlaintesWithFilters(PlainteFiltres filtres, string tri, string sens, int pageNumber, int pageSize) => throw new NotImplementedException();
            public Task<bool> createPlainte(CreatePlainteViewModel model, string userEmail) => throw new NotImplementedException();
            public Task<bool> updatePlainte(CreatePlainteViewModel model) => throw new NotImplementedException();
            public Task<string?> validerPlainte(CreatePlainteViewModel model) => throw new NotImplementedException();
            public Task AddPiecesAsync(int numPlainte, IEnumerable<PieceJointeViewModel> pieces, CancellationToken ct = default) => throw new NotImplementedException();
        }

        /// <summary>Convertisseur factice : chaque conversion dure un peu, et on mesure combien tournent en même temps.</summary>
        private sealed class FauxConvertisseur : IConvertisseurPdf
        {
            private int _enCours;
            public int Maximum { get; private set; }
            public int Appels { get; private set; }
            public Func<byte[], Exception?>? Echec { get; set; }

            public async Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default)
            {
                var n = Interlocked.Increment(ref _enCours);
                lock (this) { Appels++; Maximum = Math.Max(Maximum, n); }
                try
                {
                    await Task.Delay(150, ct);
                    var erreur = Echec?.Invoke(contenu);
                    if (erreur != null) throw erreur;
                    return new byte[] { 0x25, 0x50, 0x44, 0x46, (byte)contenu[0] };   // « %PDF » + marqueur de l'entrée
                }
                finally { Interlocked.Decrement(ref _enCours); }
            }
        }

        private sealed class FauxRecuperateur : IRecuperateurPieces
        {
            public List<PieceExport> Pieces { get; set; } = new();
            public Exception? Erreur { get; set; }
            public Task<List<PieceExport>> RecupererAsync(int numPlainte, CancellationToken ct = default) =>
                Erreur != null ? throw Erreur : Task.FromResult(Pieces);
        }

        private sealed class AssembleurEspion : IAssembleurPdf
        {
            public byte[]? PdfPlainte { get; private set; }
            public IReadOnlyList<PieceExport>? Pieces { get; private set; }
            public Task<ResultatAssemblage> AssemblerAsync(byte[] pdfPlainte, IReadOnlyList<PieceExport> pieces, CancellationToken ct = default)
            {
                PdfPlainte = pdfPlainte;
                Pieces = pieces;
                return Task.FromResult(new ResultatAssemblage(new byte[] { 9 }, Array.Empty<string>()));
            }
        }

        private static PieceExport Word(int num, byte marqueur) =>
            new(num, $"p{num}.docx", ".docx", TypePieceExport.Word, "Autre", new[] { marqueur }, null);

        private static (ExportPlainteSrv, FauxConvertisseur, FauxRecuperateur, AssembleurEspion) Creer()
        {
            var conv = new FauxConvertisseur();
            var rec = new FauxRecuperateur();
            var asm = new AssembleurEspion();
            return (new ExportPlainteSrv(new FauxPlainteSrv(), conv, rec, asm, NullLogger<ExportPlainteSrv>.Instance), conv, rec, asm);
        }

        private static readonly Plainte UnePlainte = new() { NumPlainte = 42 };

        [Fact]
        public async Task PiecesWord_SontConverties_AvantAssemblage_EtLOrdreEstConserve()
        {
            var (srv, conv, rec, asm) = Creer();
            rec.Pieces = new()
            {
                new(1, "a.pdf", ".pdf", TypePieceExport.Pdf, "Facture", new byte[] { 7 }, null),
                Word(2, 2),
                Word(3, 3),
            };

            await srv.ExporterPdfAsync(UnePlainte);

            Assert.Equal(3, conv.Appels);                                    // plainte + 2 pièces Word
            Assert.Equal(new[] { 1, 2, 3 }, asm.Pieces!.Select(p => p.NumPiece));
            Assert.All(asm.Pieces!, p => Assert.Equal(TypePieceExport.Pdf, p.Type));
            Assert.Equal(2, asm.Pieces![1].Contenu![4]);                     // le contenu converti de la pièce 2
            Assert.Equal(3, asm.Pieces![2].Contenu![4]);
            Assert.Equal(1, asm.PdfPlainte![4]);                             // le PDF de la plainte (docx = { 1 })
        }

        [Fact]
        public async Task Conversions_SeFontEnParallele()
        {
            var (srv, conv, rec, _) = Creer();
            rec.Pieces = new() { Word(1, 1), Word(2, 2), Word(3, 3) };

            await srv.ExporterPdfAsync(UnePlainte);

            Assert.True(conv.Maximum >= 3, $"Les conversions devraient se chevaucher (maximum simultané observé : {conv.Maximum}).");
        }

        [Fact]
        public async Task ConversionDeLaPlainte_ChevaucheLaLectureDesPieces()
        {
            var (srv, conv, rec, _) = Creer();
            rec.Pieces = new() { Word(1, 1) };

            await srv.ExporterPdfAsync(UnePlainte);

            Assert.Equal(2, conv.Maximum);      // plainte et pièce en même temps
        }

        [Fact]
        public async Task EchecDeConversionDUnePiece_NeBloquePasLExport_EtLaPieceEstSignalee()
        {
            var (srv, conv, rec, asm) = Creer();
            conv.Echec = c => c[0] == 2 ? new InvalidOperationException("LibreOffice a échoué") : null;
            rec.Pieces = new() { Word(1, 1), Word(2, 2) };

            var r = await srv.ExporterPdfAsync(UnePlainte);

            Assert.NotNull(r);
            Assert.True(asm.Pieces![0].EstUtilisable);
            Assert.False(asm.Pieces![1].EstUtilisable);
            Assert.Contains("conversion", asm.Pieces![1].Erreur);
        }

        [Fact]
        public async Task DelaiDepasse_EstSignaleCommeTel()
        {
            var (srv, conv, rec, asm) = Creer();
            conv.Echec = c => c[0] == 2 ? new TimeoutException() : null;
            rec.Pieces = new() { Word(2, 2) };

            await srv.ExporterPdfAsync(UnePlainte);

            Assert.Contains("délai", asm.Pieces![0].Erreur);
        }

        [Fact]
        public async Task PieceDejaEnErreur_EstTransmiseSansConversion()
        {
            var (srv, conv, rec, asm) = Creer();
            rec.Pieces = new() { new(1, "x.docx", ".docx", TypePieceExport.Word, "Autre", null, "Le fichier de cette pièce est introuvable.") };

            await srv.ExporterPdfAsync(UnePlainte);

            Assert.Equal(1, conv.Appels);       // seulement la plainte
            Assert.Equal("Le fichier de cette pièce est introuvable.", asm.Pieces![0].Erreur);
        }

        [Fact]
        public async Task EchecDeLaConversionDeLaPlainte_Leve()
        {
            var (srv, conv, rec, _) = Creer();
            conv.Echec = c => c[0] == 1 ? new InvalidOperationException("LibreOffice introuvable") : null;
            rec.Pieces = new() { Word(2, 2) };

            await Assert.ThrowsAsync<InvalidOperationException>(() => srv.ExporterPdfAsync(UnePlainte));
        }

        [Fact]
        public async Task EchecDeLaLectureDesPieces_Leve_SansLaisserLaConversionEnCours()
        {
            var (srv, conv, rec, _) = Creer();
            rec.Erreur = new IOException("disque");

            await Assert.ThrowsAsync<IOException>(() => srv.ExporterPdfAsync(UnePlainte));
            Assert.Equal(1, conv.Appels);
        }
    }
}
