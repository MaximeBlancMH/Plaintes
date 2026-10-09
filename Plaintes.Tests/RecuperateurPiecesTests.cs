using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.Export;
using Plaintes.Services.Stockage;

namespace Plaintes.Tests
{
    public class RecuperateurPiecesTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();
        private readonly string _racine = Path.Combine(Path.GetTempPath(), "plaintes-tests-" + Guid.NewGuid());
        private readonly FileSystemPieceStorage _storage;
        private readonly RecuperateurPieces _recuperateur;

        public RecuperateurPiecesTests()
        {
            _storage = new FileSystemPieceStorage(
                Options.Create(new StorageSettings { Racine = _racine, Extensions = new[] { ".pdf", ".png", ".jpg", ".docx", ".doc", ".gif" } }),
                new FakeEnv());
            _recuperateur = new RecuperateurPieces(_ctx, _storage, NullLogger<RecuperateurPieces>.Instance);
            _ctx.NaturePieces.AddRange(
                new NaturePiece { CodeNature = "FACT", LibNature = "Facture" },
                new NaturePiece { CodeNature = "AUTR", LibNature = "Autre" });
        }

        public void Dispose()
        {
            _ctx.Dispose();
            if (Directory.Exists(_racine)) Directory.Delete(_racine, true);
        }

        // Nouvelle pièce : fichier sur disque + chemin en base.
        private async Task AjouterSurDisque(int numPiece, string nom, byte[] contenu, int numPlainte = 1, string? nature = "FACT", string? precision = null, bool supprimee = false)
        {
            var stocke = await _storage.SaveAsync(new MemoryStream(contenu), nom, numPlainte);
            _ctx.PieceJointes.Add(new PieceJointe
            {
                NumPiece = numPiece, NumPlainte = numPlainte, LibPiece = nom, TypePiece = "x", TaillePiece = "1",
                CheminPiece = stocke.RelativePath, HashPiece = stocke.Sha256, CodeNature = nature, PrecisionNature = precision, SupprPj = supprimee
            });
            await _ctx.SaveChangesAsync();
        }

        // Ancienne pièce : contenu en base.
        private async Task AjouterEnBase(int numPiece, string? nom, string mime, byte[]? contenu, int numPlainte = 1)
        {
            _ctx.PieceJointes.Add(new PieceJointe { NumPiece = numPiece, NumPlainte = numPlainte, LibPiece = nom, TypePiece = mime, TaillePiece = "1", Fichier = contenu });
            await _ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task SansPieces_RenvoieUneListeVide() =>
            Assert.Empty(await _recuperateur.RecupererAsync(1));

        [Fact]
        public async Task NouvellePiece_EstLueSurLeDisque()
        {
            await AjouterSurDisque(1, "facture.pdf", new byte[] { 1, 2, 3 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.True(piece.EstUtilisable);
            Assert.Equal(new byte[] { 1, 2, 3 }, piece.Contenu);
            Assert.Equal("facture.pdf", piece.Nom);
            Assert.Equal(".pdf", piece.Extension);
            Assert.Equal(TypePieceExport.Pdf, piece.Type);
        }

        [Fact]
        public async Task AnciennePiece_EstLueEnBase()
        {
            await AjouterEnBase(1, "photo.png", "image/png", new byte[] { 9, 9 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.True(piece.EstUtilisable);
            Assert.Equal(new byte[] { 9, 9 }, piece.Contenu);
            Assert.Equal(TypePieceExport.Image, piece.Type);
        }

        [Fact]
        public async Task PiecesTrieesParNumero_SupprimeesEtAutresPlaintesExclues()
        {
            await AjouterSurDisque(3, "c.pdf", new byte[] { 1 });
            await AjouterSurDisque(1, "a.pdf", new byte[] { 1 });
            await AjouterSurDisque(2, "b.pdf", new byte[] { 1 }, supprimee: true);
            await AjouterSurDisque(4, "d.pdf", new byte[] { 1 }, numPlainte: 2);

            var pieces = await _recuperateur.RecupererAsync(1);

            Assert.Equal(new[] { 1, 3 }, pieces.Select(p => p.NumPiece));
        }

        [Fact]
        public async Task FichierAbsentDuDisque_RenvoieUneErreur_SansLever()
        {
            await AjouterSurDisque(1, "a.pdf", new byte[] { 1 });
            var chemin = _ctx.PieceJointes.Single().CheminPiece!;
            File.Delete(Path.Combine(_racine, chemin));

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.False(piece.EstUtilisable);
            Assert.Null(piece.Contenu);
            Assert.Contains("introuvable", piece.Erreur);
        }

        [Fact]
        public async Task CheminHorsRacine_RenvoieUneErreur_SansLever()
        {
            _ctx.PieceJointes.Add(new PieceJointe { NumPiece = 1, NumPlainte = 1, LibPiece = "a.pdf", TypePiece = "x", TaillePiece = "1", CheminPiece = "../../etc/passwd" });
            await _ctx.SaveChangesAsync();

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.False(piece.EstUtilisable);
            Assert.NotNull(piece.Erreur);
        }

        [Fact]
        public async Task AnciennePieceSansContenu_RenvoieUneErreur()
        {
            await AjouterEnBase(1, "a.pdf", "application/pdf", null);

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.False(piece.EstUtilisable);
            Assert.Contains("introuvable", piece.Erreur);
        }

        [Fact]
        public async Task TypeNonPrisEnCharge_RenvoieUneErreur_SansLireLeFichier()
        {
            await AjouterSurDisque(1, "animation.gif", new byte[] { 1 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.Equal(TypePieceExport.NonPris, piece.Type);
            Assert.Null(piece.Contenu);
            Assert.Contains(".gif", piece.Erreur);
        }

        [Theory]
        [InlineData("scan.PDF", ".pdf", TypePieceExport.Pdf)]
        [InlineData("photo.JPEG", ".jpeg", TypePieceExport.Image)]
        [InlineData("courrier.docx", ".docx", TypePieceExport.Word)]
        [InlineData("ancien.doc", ".doc", TypePieceExport.Word)]
        public async Task TypeEtExtension_DeduitsDuNom(string nom, string extension, TypePieceExport type)
        {
            await AjouterEnBase(1, nom, "application/octet-stream", new byte[] { 1 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.Equal(extension, piece.Extension);
            Assert.Equal(type, piece.Type);
        }

        [Theory]
        [InlineData("application/pdf", ".pdf", TypePieceExport.Pdf)]
        [InlineData("image/jpeg", ".jpg", TypePieceExport.Image)]
        [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", TypePieceExport.Word)]
        public async Task NomSansExtension_SeRabatSurLeTypeMime(string mime, string extension, TypePieceExport type)
        {
            await AjouterEnBase(1, "sans-extension", mime, new byte[] { 1 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.Equal(extension, piece.Extension);
            Assert.Equal(type, piece.Type);
        }

        [Fact]
        public async Task NomVide_UtiliseLeNumeroDeLaPiece_EtLeTypeMime()
        {
            await AjouterEnBase(42, "  ", "application/pdf", new byte[] { 1 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.Equal("piece-42", piece.Nom);
            Assert.Equal(TypePieceExport.Pdf, piece.Type);
        }

        [Fact]
        public async Task NomAvecDossierWindows_NeGardeQueLeNom()
        {
            await AjouterEnBase(1, @"C:\Users\x\facture.pdf", "application/pdf", new byte[] { 1 });

            var piece = Assert.Single(await _recuperateur.RecupererAsync(1));

            Assert.Equal("facture.pdf", piece.Nom);
        }
    }
}
