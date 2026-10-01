using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Etat
{
    public string CodEtat { get; set; } = null!;

    public string LibEtat { get; set; } = null!;

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
