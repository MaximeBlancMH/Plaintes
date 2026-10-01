using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Categorie
{
    public string CodeCat { get; set; } = null!;

    public string? LibCat { get; set; }

    public bool SupprCat { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();

    public virtual ICollection<SousCategorie> SousCategories { get; set; } = new List<SousCategorie>();
}
