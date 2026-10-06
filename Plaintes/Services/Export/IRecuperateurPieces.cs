namespace Plaintes.Services.Export
{
    public interface IRecuperateurPieces
    {
        /// <summary>
        /// Récupère les pièces jointes non supprimées d'une plainte, triées par numéro de pièce, avec leur contenu.
        /// Une pièce dont le fichier est introuvable ou d'un type non pris en charge est renvoyée avec son <c>Erreur</c> :
        /// c'est à l'appelant de décider de l'ignorer, de la signaler dans le PDF ou de refuser l'export.
        /// </summary>
        Task<List<PieceExport>> RecupererAsync(int numPlainte, CancellationToken ct = default);
    }
}
