using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Plaintes.Services.Stockage
{
    public class FileSystemPieceStorage : IPieceStorage
    {
        private readonly StorageSettings _s;
        private readonly string _racine;

        /// <summary>
        /// Constructeur de FileSystemPieceStorage. On récupère les informations essentielles à la sauvegarde des pièces jointes.
        /// </summary>
        /// <param name="s">Paramètres de stockage de fichiers</param>
        /// <param name="env">Environnement actuel. Il nous sert afin de récupérer le chemin du dossier de l'application</param>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        public FileSystemPieceStorage(IOptions<StorageSettings> s, IWebHostEnvironment env)
        {
            _s = s.Value;
            if (string.IsNullOrWhiteSpace(_s.Racine))
                throw new InvalidOperationException("Le chemin de stockage (Storage:Racine) n'est pas configuré");

            // On vérifie que le chemin n'est pas qu'un chemin compatible à Windows et qu'il est accepté sur tous les OS
            if (!OperatingSystem.IsWindows() && System.Text.RegularExpressions.Regex.IsMatch(_s.Racine, @"^[A-Za-z]:[\\/]"))
                throw new InvalidOperationException($"Storage:Racine ({_s.Racine}) est un chemin Windows, inutilisable sur ce système : indiquez un chemin Linux (ex. /var/plaintes/pieces).");

            _racine = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_s.Racine, env.ContentRootPath));
            if (!Directory.Exists(_racine))
                Directory.CreateDirectory(_racine);
        }

        /// <summary>
        /// On transforme notre chemin relatif en chemin complet pour avoir le chemin du fichier le plus sûr possible. 
        /// On refuse tout chemin qui sort de la racine
        /// </summary>
        /// <param name="path">Chemin relatif</param>
        /// <returns>Chemin du fichier</returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        private string Resolve(string path)
        {
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var full = Path.GetFullPath(Path.Combine(_racine, path));

            if (!full.StartsWith(_racine + Path.DirectorySeparatorChar, cmp))
                throw new InvalidOperationException("Chemin invalide");

            return full;
        }

        /// <summary>
        /// Fonction qui sauvegare la piece jointe sur le disque
        /// </summary>
        /// <param name="content">Contenu de la pièce jointe sous forme de Stream</param>
        /// <param name="originalName">Nom de base du fichier</param>
        /// <param name="numPlainte">Numéro de la plainte (Clef primaire)</param>
        /// <param name="checkExtension">Est ce qu'on doit vérifier l'extention</param>
        /// <param name="ct">Token d'annulation</param>
        /// <returns>Métadonnées du fichier sous forme de Record</returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
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


        /// <summary>
        /// Fonction qui ouvre un fichier depuis son chemin.
        /// </summary>
        /// <param name="path">Chemin du fichier à ouvrir</param>
        /// <returns>Stream du fichier</returns>
        /// <exception cref="FileNotFoundException">Erreur qui indique que le fichier n'a pas été trouvé</exception>
        public Stream Open(string path)
        {
            var absolutePath = Resolve(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Fichier introuvable", path);
            return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Fonction qui supprime un fichier sur le disque
        /// </summary>
        /// <param name="path">Chemin du fichier</param>
        public void Delete(string path)
        {
            var absolutePath = Resolve(path);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }

        /// <summary>
        /// Fonction qui vérifie si un fichier existe bien à ce chemin là
        /// </summary>
        /// <param name="path">Chemin du fichier</param>
        /// <returns>True si le fichier existe, False s'il n'existe pas</returns>
        public bool Exists(string path)
        {
            var absolutePath = Resolve(path);
            return File.Exists(absolutePath);
        }

        /// <summary>
        /// Fonction qui vérifie si le fichier témoin existe. Cela permet de vérifier que le dossier 
        /// est accessible et le partage opérationnel.
        /// </summary>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
        private void VerifierTemoin()
        {
            if (!string.IsNullOrWhiteSpace(_s.FichierTemoin) &&
                !File.Exists(Path.Combine(_racine, _s.FichierTemoin)))
                throw new InvalidOperationException(
                    $"Fichier témoin '{_s.FichierTemoin}' introuvable dans {_racine} : le partage n'est probablement pas monté");
        }

        /// <summary>
        /// Fonction qui teste l'écriture et la relecture d'un fichier dans notre dossier de stockage
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Erreur qui indique que l'opération demandée ne peut pas être exécutée</exception>
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
