using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Rue
{
    public int CodeRue { get; set; }

    public string? LibRue { get; set; }

    public string? Cp { get; set; }

    public string? Ville { get; set; }

    public virtual ICollection<Entree> Entrees { get; set; } = new List<Entree>();

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
