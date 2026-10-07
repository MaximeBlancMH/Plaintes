namespace Plaintes.Services.Export
{
    /// <param name="Pdf">Le PDF final : plainte, puis chaque pièce utilisable à la suite, sans les pièces inutilisables.</param>
    /// <param name="NonJointes">Une ligne par pièce qui n'a pas pu être intégrée (« nom : raison »). Vide si tout est intégré.</param>
    public record ResultatAssemblage(byte[] Pdf, IReadOnlyList<string> NonJointes);

    public interface IAssembleurPdf
    {
        Task<ResultatAssemblage> AssemblerAsync(byte[] pdfPlainte, IReadOnlyList<PieceExport> pieces, CancellationToken ct = default);
    }
}
