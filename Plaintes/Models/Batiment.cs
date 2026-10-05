using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Batiment
{
    public int IdBat { get; set; }

    public int NumCite { get; set; }

    public string NumBat { get; set; } = null!;

    public string? LibBat { get; set; }

    public string? CodeTranche { get; set; }

    public virtual Cite NumCiteNavigation { get; set; } = null!;

    public virtual ICollection<BatimentRue> BatimentRues { get; set; } = new List<BatimentRue>();
}
