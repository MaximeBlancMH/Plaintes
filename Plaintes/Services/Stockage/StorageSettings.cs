namespace Plaintes.Services.Stockage
{
    public class StorageSettings
    {
        public string Racine { get; set; } = "";
        // Taille maximale d'un fichier (octets).
        public long MaxSize { get; set; } = 20 * 1024 * 1024;
        // Taille totale maximale des pièces d'une même plainte (octets) et nombre maximal de pièces.
        public long MaxTotalSize { get; set; } = 50 * 1024 * 1024;
        public int MaxPieces { get; set; } = 10;
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public string? FichierTemoin { get; set; }

        /// <summary>Compare sans tenir compte des majuscules : "facture.PDF" est accepté si ".pdf" est autorisé.</summary>
        public bool ExtensionAutorisee(string? extension) =>
            !string.IsNullOrEmpty(extension) &&
            Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }
}
