using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Entree
{
    public int IdEntree { get; set; }

    public string NumEntree { get; set; } = null!;

    public int CodeRue { get; set; }

    public int IdBat { get; set; }

    public string? NumRue { get; set; }

    public virtual Rue CodeRueNavigation { get; set; } = null!;

    public virtual Batiment IdBatNavigation { get; set; } = null!;
}
