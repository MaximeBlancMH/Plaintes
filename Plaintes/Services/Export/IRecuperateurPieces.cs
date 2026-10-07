namespace Plaintes.Services.Export
{
    public interface IRecuperateurPieces
    {
        Task<List<PieceExport>> RecupererAsync(int numPlainte, CancellationToken ct = default);
    }
}
