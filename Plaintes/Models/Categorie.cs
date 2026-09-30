using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Categorie
{
    public string Codecat { get; set; } = null!;

    public string? Libcat { get; set; }

    public bool Supprcat { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();

    public virtual ICollection<SousCategorie> SousCategories { get; set; } = new List<SousCategorie>();
}
