using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class PartieCommune
{
    public string CodePartieCommune { get; set; } = null!;

    public string? LibPartieCommune { get; set; }

    public bool SupprPc { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
