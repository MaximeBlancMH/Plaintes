using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Batiment
{
    public int Idbat { get; set; }

    public string Numbat { get; set; } = null!;

    public int Numcite { get; set; }

    public string? Libbat { get; set; }

    public int Idtranche { get; set; }

    public virtual ICollection<Entree> Entrees { get; set; } = new List<Entree>();

    public virtual Tranche IdtrancheNavigation { get; set; } = null!;

    public virtual Cite NumciteNavigation { get; set; } = null!;
}
