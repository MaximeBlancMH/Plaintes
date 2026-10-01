using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class SousCategorie
{
    public string CodeSousCat { get; set; } = null!;

    public string CodeCat { get; set; } = null!;

    public string? LibSousCat { get; set; }

    public bool SupprSousCat { get; set; }

    public virtual Categorie CodeCatNavigation { get; set; } = null!;

    public virtual ICollection<Nuisance> Nuisances { get; set; } = new List<Nuisance>();
}
