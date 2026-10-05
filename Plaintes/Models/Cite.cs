using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Cite
{
    public int NumCite { get; set; }

    public string? LibCite { get; set; }

    public string? Secteur { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();

    public virtual ICollection<Batiment> Batiments { get; set; } = new List<Batiment>();

    public virtual ICollection<Rue> Rues { get; set; } = new List<Rue>();
}
