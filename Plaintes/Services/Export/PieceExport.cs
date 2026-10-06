namespace Plaintes.Services.Export
{
    /// <summary>Comment une pièce jointe sera intégrée au PDF d'export.</summary>
    public enum TypePieceExport
    {
        Pdf,      // reprise telle quelle
        Image,    // jpg / png : une page PDF par image
        Word,     // doc / docx : convertie en PDF par LibreOffice
        NonPris   // autre type : non intégrable
    }

    /// <summary>
    /// Une pièce jointe prête à être intégrée à l'export : son contenu en mémoire, ou la raison pour laquelle il est indisponible.
    /// </summary>
    /// <param name="Nom">Nom d'origine du fichier (ou "piece-N" s'il est vide).</param>
    /// <param name="Extension">Extension en minuscules avec le point (".pdf"), déduite du nom puis, à défaut, du type MIME enregistré.</param>
    /// <param name="Nature">Libellé de la nature ("Facture", "Autre : courrier"...), pour le titre de l'annexe.</param>
    /// <param name="Contenu">Octets du fichier ; null si <paramref name="Erreur"/> est renseignée.</param>
    /// <param name="Erreur">Raison pour laquelle la pièce ne peut pas être intégrée ; null si tout va bien.</param>
    public record PieceExport(int NumPiece, string Nom, string Extension, TypePieceExport Type, string Nature, byte[]? Contenu, string? Erreur)
    {
        public bool EstUtilisable => Erreur is null && Contenu is not null;
    }
}
