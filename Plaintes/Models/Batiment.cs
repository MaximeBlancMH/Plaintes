using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Batiment
{
    public int IdBat { get; set; }

    public string NumBat { get; set; } = null!;

    public int NumCite { get; set; }

    public string? LibBat { get; set; }

    public int IdTranche { get; set; }

    public virtual ICollection<Entree> Entrees { get; set; } = new List<Entree>();

    public virtual Tranche IdTrancheNavigation { get; set; } = null!;

    public virtual Cite NumCiteNavigation { get; set; } = null!;
}
