using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Tranche
{
    public string Codetranche { get; set; } = null!;

    public int Numcite { get; set; }

    public string? Libtranche { get; set; }

    public int Idtranche { get; set; }

    public virtual ICollection<Batiment> Batiments { get; set; } = new List<Batiment>();

    public virtual Cite NumciteNavigation { get; set; } = null!;
}
