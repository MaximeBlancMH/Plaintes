namespace Plaintes.Services.Stockage
{
    public class StorageSettings
    {
        public string Racine { get; set; } = "";
        public long MaxSize { get; set; } = 20 * 1024 * 1024;
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public string? FichierTemoin { get; set; }
    }
}
