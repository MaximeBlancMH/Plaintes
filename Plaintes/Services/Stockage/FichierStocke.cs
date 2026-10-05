namespace Plaintes.Services.Stockage
{
    public record FichierStocke(string RelativePath, string Sha256, long Size);
}
