using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Cite
{
    public int Numcite { get; set; }

    public string? Libcite { get; set; }

    public string? Secteur { get; set; }

    public virtual ICollection<Batiment> Batiments { get; set; } = new List<Batiment>();

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();

    public virtual ICollection<Tranche> Tranches { get; set; } = new List<Tranche>();
}
