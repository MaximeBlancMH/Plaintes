using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace Plaintes.Services.Export
{
    public class LibreOfficeConvertisseurPdf : IConvertisseurPdf, IDisposable
    {
        private static readonly string[] ExtensionsAcceptees = { ".doc", ".docx" };

        private readonly ExportSettings _s;
        private readonly ILogger<LibreOfficeConvertisseurPdf> _logger;
        private readonly SemaphoreSlim _places;
        // Stack (Last in, First out), la place ajoutée en dernière est réutilisé en première
        private readonly ConcurrentStack<Place> _libres = new();
        private readonly List<Place> _toutes = new();

        /// <summary>
        /// Classe qui décrit une "place" : c'est un profil LibreOffice avec son verrou
        /// </summary>
        private sealed class Place
        {
            public string? Profil;       // dossier du profil LibreOffice, choisi à la première utilisation
            public FileStream? Verrou;   // verrou exclusif sur le profil, gardé jusqu'à l'arrêt
        }

        /// <summary>
        /// Constructeur qui permet d'initialiser les paramètres de l'export ainsi que la stack de places
        /// </summary>
        /// <param name="s"></param>
        /// <param name="logger"></param>
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

        /// <summary>
        /// Fonction qui convertit un fichier en pdf
        /// </summary>
        /// <param name="contenu">Octets du fichier</param>
        /// <param name="extension">Extension du fichier</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Octets du nouveau fichier pdf</returns>
        /// <exception cref="ArgumentException">Erreur qui indique qu'un argument de la méthode n'est pas correct</exception>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        public async Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default)
        {
            // On renvoie une erreur si le contenu est vide ou null
            ArgumentNullException.ThrowIfNull(contenu);
            if (contenu.Length == 0)
                throw new ArgumentException("Le document à convertir est vide.", nameof(contenu));

            // On vérifie si l'extension du document est autorisée. Si non, on renvoie une erreur
            extension = (extension ?? "").ToLowerInvariant();
            if (!ExtensionsAcceptees.Contains(extension))
                throw new ArgumentException($"Extension non convertible : « {extension} » (acceptées : {string.Join(", ", ExtensionsAcceptees)}).", nameof(extension));

            // On récupère une place libre, s'il n'y en a pas, on attend qu'une se libère
            await _places.WaitAsync(ct);

            // On retire une place de libre où une place = un profil LibreOffice + son verrou
            _libres.TryPop(out var place);

            var dossier = Path.Combine(DossierRacine, "plaintes-pdf-" + Guid.NewGuid().ToString("N"));
            var reussi = false;
            try
            {
                // On crée le dossier temporaire
                Directory.CreateDirectory(dossier);
                // On crée le chemin pour le fichier d'entrée ainsi que celui de sortie
                var entree = Path.Combine(dossier, "entree" + extension);
                var sortie = Path.Combine(dossier, "entree.pdf");

                // On écrit les octets de notre docx dans le fichier temporaire
                await File.WriteAllBytesAsync(entree, contenu, ct);

                // On lance la conversion avec LibreOffice
                await LancerLibreOfficeAsync(entree, dossier, ProfilDe(place!), ct);

                // Si le pdf n'a pas été créé, on renvoie une erreur
                if (!File.Exists(sortie))
                    throw new InvalidOperationException("LibreOffice n'a pas produit de PDF.");

                // On récupère les octets du pdf et on le renvoie
                var pdf = await File.ReadAllBytesAsync(sortie, ct);
                reussi = true;
                return pdf;
            }
            finally
            {
                // On supprime le dossier temporaire
                try { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Impossible de supprimer le dossier temporaire {Dossier}.", dossier); }
                // Si la conversion a échoué, on supprime le profil pour ne pas le laisser dans un possible mauvais état.
                if (!reussi && place!.Profil is not null) SupprimerProfil(place.Profil);
                _libres.Push(place!);
                _places.Release();
            }
        }

        /// <summary>
        /// Fonction qui choisit quel profil utiliser. Si la place en possède déjà un, on le renvoie sinon, on choisit le premier qui est libre
        /// </summary>
        /// <param name="place">Place qui va accueillir le profil</param>
        /// <returns>Le profil sous forme de string</returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        private string ProfilDe(Place place)
        {
            // Si la place a déjà un profil, on le retourne
            if (place.Profil is not null) return place.Profil;

            // On crée le dossier temporaire
            Directory.CreateDirectory(DossierRacine);

            // On parcourt un grand nombre de valeurs pour trouver un profil libre
            for (int i = 0; i < 100; i++)
            {
                // On assemble le chemin du profil n°i
                var profil = Path.Combine(DossierRacine, $"plaintes-lo-profil-{i}");
                try
                {
                    // On lie le verrou et le profil à la place
                    place.Verrou = new FileStream(profil + ".verrou", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    place.Profil = profil;
                    // Si le programme n'a pas envoyé d'erreur, alors le profil est libre et on peut le renvoyer
                    return profil;
                }
                catch (IOException) { /* pris par une autre place ou une autre instance : on essaie le suivant */ }
            }
            throw new InvalidOperationException("Aucun dossier de profil LibreOffice disponible.");
        }

        /// <summary>
        /// Fonction qui supprime un profil Libre Office.
        /// </summary>
        /// <param name="profil">Chemin complet du dossier</param>
        private void SupprimerProfil(string profil)
        {
            try { if (Directory.Exists(profil)) Directory.Delete(profil, true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Impossible de supprimer le profil LibreOffice {Profil}.", profil); }
        }

        /// <summary>
        /// Fonction qui libère les verrous et nettoie le compteur de places
        /// </summary>
        public void Dispose()
        {
            foreach (var place in _toutes) place.Verrou?.Dispose();
            _places.Dispose();
        }

        /// <summary>
        /// Fonction qui lance la commande de conversion des fichiers avec LibreOffice
        /// </summary>
        /// <param name="entree">Chemin du fichier d'entrée</param>
        /// <param name="dossier">Chemin du dossier de travail</param>
        /// <param name="profil">Chemin du dossier de profil LibreOffice de la place utilisée</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        /// <exception cref="TimeoutException">Erreur qui indique que le traitement a pris trop de temps</exception>
        private async Task LancerLibreOfficeAsync(string entree, string dossier, string profil, CancellationToken ct)
        {
            // On crée un objet qui va contenir les informations de démarrage de notre process LibreOffice ainsi que la commande de conversion.
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
                // On démarre la process LibreOffice
                process = Process.Start(psi) ?? throw new InvalidOperationException("LibreOffice n'a pas pu être lancé.");
            }
            catch (Win32Exception ex)
            {
                // On renvoie une erreur qui indique que LibreOffice n'est pas installé ou mal indiqué
                throw new InvalidOperationException(
                    $"LibreOffice est introuvable (« {_s.SofficePath} ») : installez-le ou renseignez Export:SofficePath.", ex);
            }

            using (process)
            {
                // On lit les flux pendant le processus pour éviter de bloquer le processus
                var sortieStd = process.StandardOutput.ReadToEndAsync(ct);
                var sortieErr = process.StandardError.ReadToEndAsync(ct);

                // On met en place le délai de sécurité afin d'annuler le traitement si le temps dépasse la durée max indiquée
                using var delai = CancellationTokenSource.CreateLinkedTokenSource(ct);
                delai.CancelAfter(TimeSpan.FromSeconds(_s.TimeoutSeconds));
                try
                {
                    // On attend le process avec soit la fin normale soit la fin anticipée à cause du délai
                    await process.WaitForExitAsync(delai.Token);
                }
                catch (OperationCanceledException)
                {
                    // L'opération a été annulée, on tue le process et on renvoie l'erreur
                    try { process.Kill(entireProcessTree: true); } catch { /* déjà terminé */ }
                    if (ct.IsCancellationRequested) throw;
                    throw new TimeoutException($"La conversion PDF a dépassé {_s.TimeoutSeconds} s et a été interrompue.");
                }

                var erreurs = await sortieErr;
                _ = await sortieStd;
                // On renvoie l'erreur si la conversion a échoué
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"LibreOffice a échoué (code {process.ExitCode}) : {erreurs.Trim()}");
            }
        }
    }
}
