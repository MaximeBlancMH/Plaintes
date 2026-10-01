using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Composer
{
    public int NumDetail { get; set; }

    public string CodeNuisance { get; set; } = null!;

    public int NumPlainte { get; set; }

    public int? Cout { get; set; }

    public bool SupprDet { get; set; }

    public virtual Nuisance CodeNuisanceNavigation { get; set; } = null!;

    public virtual Plainte NumPlainteNavigation { get; set; } = null!;
}
