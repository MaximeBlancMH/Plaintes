using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Tranche
{
    public string CodeTranche { get; set; } = null!;

    public int NumCite { get; set; }

    public string? LibTranche { get; set; }

    public int IdTranche { get; set; }

    public virtual ICollection<Batiment> Batiments { get; set; } = new List<Batiment>();

    public virtual Cite NumCiteNavigation { get; set; } = null!;
}
