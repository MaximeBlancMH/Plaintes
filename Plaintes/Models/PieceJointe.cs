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

    public virtual Plainte NumPlainteNavigation { get; set; } = null!;
}
