using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class PieceJointe
{
    public int Numpiece { get; set; }

    public int Numplainte { get; set; }

    public string? Libpiece { get; set; }

    public byte[]? Fichier { get; set; }

    public string Typepiece { get; set; } = null!;

    public string Taillepiece { get; set; } = null!;

    public bool Supprpj { get; set; }

    public virtual Plainte NumplainteNavigation { get; set; } = null!;
}
