namespace Plaintes.Models
{
    /// <summary>
    /// Un filtre appliqué, tel qu'il est rappelé dans un export Excel (ex. « Cité (contient) » : « Pins »)
    /// </summary>
    public record FiltreExport(string Libelle, string Valeur);

    /// <summary>
    /// Données de l'export Excel de la page Dossiers : toutes les plaintes filtrées et triées (sans pagination),
    /// le nombre de pièces jointes de chacune et le rappel des filtres
    /// </summary>
    public record DossiersExport(List<Plainte> Plaintes, Dictionary<int, int> NbPieces, List<FiltreExport> Filtres);
}
