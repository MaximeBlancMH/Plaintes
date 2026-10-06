using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace Plaintes.Services.Export
{
    /// <summary>
    /// Convertit un .doc/.docx en PDF en lançant LibreOffice.
    /// - Le démarrage de LibreOffice est ce qui coûte le plus cher, surtout avec un profil neuf (≈ 5,5 s contre ≈ 2,8 s avec un profil
    ///   déjà utilisé). Le profil est donc conservé d'une conversion à l'autre, dans un dossier fixe du dossier temporaire.
    /// - Deux LibreOffice ne peuvent pas partager un profil : on dispose de <see cref="ExportSettings.ConversionsSimultanees"/> « places »,
    ///   chacune avec son propre profil. Une place est réservée à un seul processus (fichier verrou), même si deux instances de
    ///   l'application tournent sur la machine.
    /// - Si une conversion échoue (délai dépassé, plantage), le profil de la place est supprimé : il sera recréé proprement.
    /// - Chaque conversion travaille dans son propre dossier temporaire (nom GUID, jamais le nom d'origine), supprimé ensuite.
    /// </summary>
    public class LibreOfficeConvertisseurPdf : IConvertisseurPdf, IDisposable
    {
        private static readonly string[] ExtensionsAcceptees = { ".doc", ".docx" };

        private readonly ExportSettings _s;
        private readonly ILogger<LibreOfficeConvertisseurPdf> _logger;
        private readonly SemaphoreSlim _places;
        // Pile (dernier sorti, premier servi) : la place utilisée en dernier, donc la plus « chaude », est réutilisée en premier.
        private readonly ConcurrentStack<Place> _libres = new();
        private readonly List<Place> _toutes = new();

        private sealed class Place
        {
            public string? Profil;       // dossier du profil LibreOffice, choisi à la première utilisation
            public FileStream? Verrou;   // verrou exclusif sur le profil, gardé jusqu'à l'arrêt
        }

        public LibreOfficeConvertisseurPdf(IOptions<ExportSettings> s, ILogger<LibreOfficeConvertisseurPdf> logger)
        {
            _s = s.Value;
            _logger = logger;
            var nb = Math.Max(1, _s.ConversionsSimultanees);
            _places = new SemaphoreSlim(nb, nb);
            for (int i = 0; i < nb; i++)
            {
                var place = new Place();
                _toutes.Add(place);
                _libres.Push(place);
            }
        }

        private string DossierRacine => string.IsNullOrWhiteSpace(_s.DossierTemporaire) ? Path.GetTempPath() : _s.DossierTemporaire;

        public async Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(contenu);
            if (contenu.Length == 0)
                throw new ArgumentException("Le document à convertir est vide.", nameof(contenu));
            extension = (extension ?? "").ToLowerInvariant();
            if (!ExtensionsAcceptees.Contains(extension))
                throw new ArgumentException($"Extension non convertible : « {extension} » (acceptées : {string.Join(", ", ExtensionsAcceptees)}).", nameof(extension));

            await _places.WaitAsync(ct);
            _libres.TryPop(out var place);
            var dossier = Path.Combine(DossierRacine, "plaintes-pdf-" + Guid.NewGuid().ToString("N"));
            var reussi = false;
            try
            {
                Directory.CreateDirectory(dossier);
                var entree = Path.Combine(dossier, "entree" + extension);
                var sortie = Path.Combine(dossier, "entree.pdf");
                await File.WriteAllBytesAsync(entree, contenu, ct);

                await LancerLibreOfficeAsync(entree, dossier, ProfilDe(place!), ct);

                if (!File.Exists(sortie))
                    throw new InvalidOperationException("LibreOffice n'a pas produit de PDF.");
                var pdf = await File.ReadAllBytesAsync(sortie, ct);
                reussi = true;
                return pdf;
            }
            finally
            {
                try { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Impossible de supprimer le dossier temporaire {Dossier}.", dossier); }
                // Après un échec, le profil a pu être laissé dans un état douteux : on le supprime, il sera recréé.
                if (!reussi && place!.Profil is not null) SupprimerProfil(place.Profil);
                _libres.Push(place!);
                _places.Release();
            }
        }

        // Choisit (une fois par place) un dossier de profil libre : le premier dont le fichier verrou n'est pris par personne.
        private string ProfilDe(Place place)
        {
            if (place.Profil is not null) return place.Profil;
            Directory.CreateDirectory(DossierRacine);
            for (int i = 0; i < 100; i++)
            {
                var profil = Path.Combine(DossierRacine, $"plaintes-lo-profil-{i}");
                try
                {
                    place.Verrou = new FileStream(profil + ".verrou", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    place.Profil = profil;
                    return profil;
                }
                catch (IOException) { /* pris par une autre place ou une autre instance : on essaie le suivant */ }
            }
            throw new InvalidOperationException("Aucun dossier de profil LibreOffice disponible.");
        }

        private void SupprimerProfil(string profil)
        {
            try { if (Directory.Exists(profil)) Directory.Delete(profil, true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Impossible de supprimer le profil LibreOffice {Profil}.", profil); }
        }

        public void Dispose()
        {
            foreach (var place in _toutes) place.Verrou?.Dispose();
            _places.Dispose();
        }

        private async Task LancerLibreOfficeAsync(string entree, string dossier, string profil, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(_s.SofficePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-env:UserInstallation=" + new Uri(profil + Path.DirectorySeparatorChar).AbsoluteUri);
            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add("pdf:writer_pdf_Export");
            psi.ArgumentList.Add("--outdir");
            psi.ArgumentList.Add(dossier);
            psi.ArgumentList.Add(entree);
            psi.Environment["HOME"] = dossier;

            Process process;
            try
            {
                process = Process.Start(psi) ?? throw new InvalidOperationException("LibreOffice n'a pas pu être lancé.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"LibreOffice est introuvable (« {_s.SofficePath} ») : installez-le ou renseignez Export:SofficePath.", ex);
            }

            using (process)
            {
                // Les flux doivent être lus pendant l'exécution, sinon un tampon plein bloque le processus.
                var sortieStd = process.StandardOutput.ReadToEndAsync(ct);
                var sortieErr = process.StandardError.ReadToEndAsync(ct);

                using var delai = CancellationTokenSource.CreateLinkedTokenSource(ct);
                delai.CancelAfter(TimeSpan.FromSeconds(_s.TimeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(delai.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* déjà terminé */ }
                    if (ct.IsCancellationRequested) throw;
                    throw new TimeoutException($"La conversion PDF a dépassé {_s.TimeoutSeconds} s et a été interrompue.");
                }

                var erreurs = await sortieErr;
                _ = await sortieStd;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"LibreOffice a échoué (code {process.ExitCode}) : {erreurs.Trim()}");
            }
        }
    }
}
