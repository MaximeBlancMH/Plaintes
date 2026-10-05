namespace Plaintes.Services.Stockage
{
    public interface IPieceStorage
    {
        Task<FichierStocke> SaveAsync(Stream content, string originalName, int numPlainte,
                                    bool checkExtension = true, CancellationToken ct = default);
        Stream Open(string path);
        void Delete(string path);
        bool Exists(string path);
        Task CheckAsync();
    }
}
