using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class PieceJointe
{
    public int NumPiece { get; set; }

    public int NumPlainte { get; set; }

    public string? LibPiece { get; set; }

    public byte[]? Fichier { get; set; }

    public string TypePiece { get; set; } = null!;

    public string TaillePiece { get; set; } = null!;

    public bool SupprPj { get; set; }

    public string? CodeNature { get; set; }

    public string? PrecisionNature { get; set; }

    public string? CheminPiece { get; set; }

    public string? HashPiece { get; set; }

    public virtual NaturePiece? CodeNatureNavigation { get; set; }

    public virtual Plainte NumPlainteNavigation { get; set; } = null!;
}
