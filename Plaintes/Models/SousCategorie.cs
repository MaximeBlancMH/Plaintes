using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class SousCategorie
{
    public string Codesouscat { get; set; } = null!;

    public string Codecat { get; set; } = null!;

    public string? Libsouscat { get; set; }

    public bool Supprsouscat { get; set; }

    public virtual Categorie CodecatNavigation { get; set; } = null!;

    public virtual ICollection<Nuisance> Nuisances { get; set; } = new List<Nuisance>();
}
