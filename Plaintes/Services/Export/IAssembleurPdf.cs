namespace Plaintes.Services.Export
{
    /// <param name="Pdf">Le PDF final : plainte, puis chaque pièce utilisable à la suite, sans les pièces inutilisables.</param>
    /// <param name="NonJointes">Une ligne par pièce qui n'a pas pu être intégrée (« nom : raison »). Vide si tout est intégré.</param>
    public record ResultatAssemblage(byte[] Pdf, IReadOnlyList<string> NonJointes);

    public interface IAssembleurPdf
    {
        /// <summary>
        /// Concatène le PDF de la plainte et les pièces jointes, dans l'ordre de la liste. PDF repris tels quels, images posées
        /// sur une page A4, Word convertis en PDF. Une pièce inutilisable (erreur de lecture, type non pris en charge, PDF protégé,
        /// conversion échouée) n'interrompt pas l'export : elle est ignorée (journalisée) et listée dans <see cref="ResultatAssemblage.NonJointes"/>.
        /// </summary>
        Task<ResultatAssemblage> AssemblerAsync(byte[] pdfPlainte, IReadOnlyList<PieceExport> pieces, CancellationToken ct = default);
    }
}
