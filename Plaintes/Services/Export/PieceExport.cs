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
    /// <param name="Nom">Nom d'origine du fichier</param>
    /// <param name="Extension">Extension du fichier</param>
    /// <param name="Contenu">Octets du fichier</param>
    /// <param name="Erreur">Raison pour laquelle la pièce ne peut pas être intégrée, null si tout va bien.</param>
    public record PieceExport(int NumPiece, string Nom, string Extension, TypePieceExport Type, byte[]? Contenu, string? Erreur)
    {
        public bool EstUtilisable => Erreur is null && Contenu is not null;
    }
}
