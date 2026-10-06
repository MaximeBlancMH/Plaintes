using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Plaintes.Services.Stockage
{
    public class FileSystemPieceStorage : IPieceStorage
    {
        private readonly StorageSettings _s;
        private readonly string _racine;

        public FileSystemPieceStorage(IOptions<StorageSettings> s, IWebHostEnvironment env)
        {
            _s = s.Value;
            if (string.IsNullOrWhiteSpace(_s.Racine))
                throw new InvalidOperationException("Le chemin de stockage (Storage:Racine) n'est pas configuré");

            // Sous Linux, "C:/..." n'est pas un chemin absolu : il serait pris pour un dossier relatif nommé "C:" et les fichiers
            // seraient écrits au mauvais endroit sans erreur. On refuse donc explicitement ce cas.
            if (!OperatingSystem.IsWindows() && System.Text.RegularExpressions.Regex.IsMatch(_s.Racine, @"^[A-Za-z]:[\\/]"))
                throw new InvalidOperationException($"Storage:Racine ({_s.Racine}) est un chemin Windows, inutilisable sur ce système : indiquez un chemin Linux (ex. /var/plaintes/pieces).");

            _racine = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_s.Racine, env.ContentRootPath));
            if (!Directory.Exists(_racine))
                Directory.CreateDirectory(_racine);
        }

        private string Resolve(string path)
        {
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var full = Path.GetFullPath(Path.Combine(_racine, path));

            if (!full.StartsWith(_racine + Path.DirectorySeparatorChar, cmp))
                throw new InvalidOperationException("Chemin invalide");

            return full;
        }

        public async Task<FichierStocke> SaveAsync(Stream content, string originalName, int numPlainte,
                                        bool checkExtension = true, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            if (checkExtension && !_s.ExtensionAutorisee(ext))
                throw new InvalidOperationException($"Extension de fichier non autorisée : {ext}");

            VerifierTemoin();

            // Toujours des "/" (et non Path.Combine) : le chemin est stocké en base, il doit être identique sous Windows et Linux.
            var relativePath = $"{numPlainte}/{Guid.NewGuid()}{ext}";
            var absolutePath = Resolve(relativePath);

            if (!Directory.Exists(Path.GetDirectoryName(absolutePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var tampon = new byte[81920];
            long total = 0;

            try
            {
                await using var fs = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, tampon.Length, true);

                int read;
                while ((read = await content.ReadAsync(tampon, ct)) > 0)
                {
                    total += read;
                    if (total > _s.MaxSize) throw new InvalidOperationException("Fichier trop volumineux");
                    sha.AppendData(tampon, 0, read);
                    await fs.WriteAsync(tampon.AsMemory(0, read), ct);
                }
                if (total == 0) throw new InvalidOperationException("Fichier vide");
            }
            catch
            {
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
                throw;
            }

            return new FichierStocke(
                relativePath,
                Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant(),
                total);
        }

        public Stream Open(string path)
        {
            var absolutePath = Resolve(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Fichier introuvable", path);
            return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Delete(string path)
        {
            var absolutePath = Resolve(path);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }

        public bool Exists(string path)
        {
            var absolutePath = Resolve(path);
            return File.Exists(absolutePath);
        }

        // Si le partage est démonté, le dossier racine peut quand même exister (en local) : sans fichier témoin on écrirait
        // sur le disque du serveur, et les fichiers disparaîtraient au remontage. Vérifié avant chaque écriture.
        private void VerifierTemoin()
        {
            if (!string.IsNullOrWhiteSpace(_s.FichierTemoin) &&
                !File.Exists(Path.Combine(_racine, _s.FichierTemoin)))
                throw new InvalidOperationException(
                    $"Fichier témoin '{_s.FichierTemoin}' introuvable dans {_racine} : le partage n'est probablement pas monté");
        }

        public async Task CheckAsync()
        {
            // 1. le partage est-il vraiment monté ?
            VerifierTemoin();

            // 2. peut-on écrire et relire ?
            var dossier = Path.Combine(_racine, ".sante");
            Directory.CreateDirectory(dossier);
            var fichier = Path.Combine(dossier, $"{Guid.NewGuid():N}.tmp");

            await File.WriteAllTextAsync(fichier, "ok");
            var relu = await File.ReadAllTextAsync(fichier);
            File.Delete(fichier);

            if (relu != "ok")
                throw new InvalidOperationException("Le stockage ne relit pas ce qui y est écrit");
            
            Directory.Delete(dossier, true);  
        }
    }
}
