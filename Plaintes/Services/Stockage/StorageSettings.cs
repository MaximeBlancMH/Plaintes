namespace Plaintes.Services.Stockage
{
    public class StorageSettings
    {
        public string Racine { get; set; } = "";
        // Taille maximale d'un fichier (octets).
        public long MaxSize { get; set; } = 20 * 1024 * 1024;
        // Taille maximale totale des pièces jointes d'une plainte (octets) pour chaque envoi
        public long MaxTotalSize { get; set; } = 50 * 1024 * 1024;
        public int MaxPieces { get; set; } = 10;
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public string? FichierTemoin { get; set; }

        /// <summary>
        /// Vérifie que l'extension demandée soit bien autorisée
        /// </summary>
        /// <param name="extension">Extension du nouveau fichier</param>
        /// <returns></returns>
        public bool ExtensionAutorisee(string? extension) =>
            !string.IsNullOrEmpty(extension) &&
            Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }
}
