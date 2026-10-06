using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plaintes.Services.Export;
using System.Diagnostics;
using System.Text;

namespace Plaintes.Tests
{
    /// <summary>Test réel, ignoré automatiquement si LibreOffice n'est pas installé (ou si Export:SofficePath n'est pas trouvé).</summary>
    public sealed class SofficeFactAttribute : FactAttribute
    {
        public SofficeFactAttribute()
        {
            if (!ConvertisseurPdfTests.SofficeDisponible()) Skip = "LibreOffice (soffice) n'est pas installé sur cette machine.";
        }
    }

    public class ConvertisseurPdfTests
    {
        private static readonly Lazy<string?> _soffice = new(TrouverSoffice);

        /// <summary>Chemin complet de soffice, ou null s'il est introuvable. Aucun processus n'est lancé (LibreOffice ne rend pas la main sur --version).</summary>
        public static string? SofficeChemin => _soffice.Value;

        public static bool SofficeDisponible() => SofficeChemin != null;

        private static string? TrouverSoffice()
        {
            var noms = OperatingSystem.IsWindows() ? new[] { "soffice.exe" } : new[] { "soffice" };
            var dossiers = new List<string>();

            var configure = Environment.GetEnvironmentVariable("Export__SofficePath");
            if (!string.IsNullOrWhiteSpace(configure))
            {
                if (File.Exists(configure)) return configure;
                dossiers.Add(configure);
            }

            dossiers.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

            if (OperatingSystem.IsWindows())
            {
                foreach (var racine in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
                    if (!string.IsNullOrEmpty(racine)) dossiers.Add(Path.Combine(racine, "LibreOffice", "program"));
            }
            else
            {
                dossiers.AddRange(new[] { "/usr/bin", "/usr/local/bin", "/opt/libreoffice/program" });
            }

            foreach (var d in dossiers)
                foreach (var n in noms)
                {
                    try
                    {
                        var c = Path.Combine(d.Trim().Trim('"'), n);
                        if (File.Exists(c)) return c;
                    }
                    catch (ArgumentException) { }
                }
            return null;
        }

        private static LibreOfficeConvertisseurPdf Creer(string? soffice = null, int timeout = 60, string? dossierTemporaire = null) =>
            new(Options.Create(new ExportSettings { SofficePath = soffice ?? SofficeChemin ?? "soffice", TimeoutSeconds = timeout, DossierTemporaire = dossierTemporaire }),
                NullLogger<LibreOfficeConvertisseurPdf>.Instance);

        // Dossier temporaire propre à un test : les autres tests (exécutés en parallèle) y créent et suppriment leurs dossiers
        // dans le dossier temporaire partagé, ce qui fausserait un comptage fait là.
        private static string DossierPrive()
        {
            var d = Path.Combine(Path.GetTempPath(), "plaintes-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private static byte[] DocxMinimal(string texte)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body(new Paragraph(new Run(new Text(texte)))));
            }
            return ms.ToArray();
        }

        [Fact]
        public async Task Convertir_DocumentVide_Refuse() =>
            await Assert.ThrowsAsync<ArgumentException>(() => Creer().ConvertirEnPdfAsync(Array.Empty<byte>(), ".docx"));

        [Theory]
        [InlineData(".exe")]
        [InlineData(".pdf")]
        [InlineData("")]
        [InlineData("docx")]
        public async Task Convertir_ExtensionNonAcceptee_Refuse(string extension) =>
            await Assert.ThrowsAsync<ArgumentException>(() => Creer().ConvertirEnPdfAsync(new byte[] { 1 }, extension));

        [Fact]
        public async Task Convertir_LibreOfficeIntrouvable_RenvoieUnMessageClair()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Creer(soffice: Path.Combine(Path.GetTempPath(), "soffice-inexistant-" + Guid.NewGuid())).ConvertirEnPdfAsync(new byte[] { 1 }, ".docx"));

            Assert.Contains("Export:SofficePath", ex.Message);
        }

        [Fact]
        public async Task Convertir_LibreOfficeIntrouvable_NeLaissePasDeDossierTemporaire()
        {
            var prive = DossierPrive();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Creer(soffice: Path.Combine(Path.GetTempPath(), "soffice-inexistant-" + Guid.NewGuid()), dossierTemporaire: prive).ConvertirEnPdfAsync(new byte[] { 1 }, ".docx"));

            Assert.Empty(Directory.GetDirectories(prive, "plaintes-pdf-*"));
        }

        [SofficeFact]
        public async Task Convertir_DocxReel_ProduitUnPdf_EtNettoieLeDisque()
        {
            var prive = DossierPrive();

            var pdf = await Creer(timeout: 120, dossierTemporaire: prive).ConvertirEnPdfAsync(DocxMinimal("Bonjour"), ".docx");

            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
            Assert.Empty(Directory.GetDirectories(prive, "plaintes-pdf-*"));
        }

        [SofficeFact]
        public async Task Convertir_LeProfilEstConserve_EntreDeuxConversions()
        {
            var prive = DossierPrive();
            var convertisseur = Creer(timeout: 120, dossierTemporaire: prive);

            await convertisseur.ConvertirEnPdfAsync(DocxMinimal("A"), ".docx");
            await convertisseur.ConvertirEnPdfAsync(DocxMinimal("B"), ".docx");

            // Un seul profil créé et réutilisé (c'est ce qui rend la 2e conversion plus rapide), et aucun dossier de travail restant.
            Assert.Single(Directory.GetDirectories(prive, "plaintes-lo-profil-*"));
            Assert.Empty(Directory.GetDirectories(prive, "plaintes-pdf-*"));
        }

        [SofficeFact]
        public async Task Convertir_PlusieursConversionsSimultanees_UtilisentUnProfilChacune()
        {
            var prive = DossierPrive();
            var convertisseur = new LibreOfficeConvertisseurPdf(
                Options.Create(new ExportSettings { SofficePath = SofficeChemin ?? "soffice", TimeoutSeconds = 120, DossierTemporaire = prive, ConversionsSimultanees = 2 }),
                NullLogger<LibreOfficeConvertisseurPdf>.Instance);

            var pdfs = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => convertisseur.ConvertirEnPdfAsync(DocxMinimal("D" + i), ".docx")));

            Assert.All(pdfs, p => Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(p, 0, 5)));
            Assert.Equal(2, Directory.GetDirectories(prive, "plaintes-lo-profil-*").Length);   // 4 conversions, 2 places
        }

        [SofficeFact]
        public async Task Convertir_DeuxConversionsSimultanees_ReussissentToutesLesDeux()
        {
            var convertisseur = Creer(timeout: 120);

            var pdfs = await Task.WhenAll(
                convertisseur.ConvertirEnPdfAsync(DocxMinimal("A"), ".docx"),
                convertisseur.ConvertirEnPdfAsync(DocxMinimal("B"), ".docx"));

            Assert.All(pdfs, p => Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(p, 0, 5)));
        }
    }
}
