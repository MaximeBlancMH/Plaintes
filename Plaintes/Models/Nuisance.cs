using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Nuisance
{
    public string CodeNuisance { get; set; } = null!;

    public string CodeSousCat { get; set; } = null!;

    public string? LibNuisance { get; set; }

    public bool SupprNuisance { get; set; }

    public virtual SousCategorie CodeSousCatNavigation { get; set; } = null!;

    public virtual ICollection<Composer> Composers { get; set; } = new List<Composer>();
}
