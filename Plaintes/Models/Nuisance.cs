using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Nuisance
{
    public string Codenuisance { get; set; } = null!;

    public string Codesouscat { get; set; } = null!;

    public string? Libnuisance { get; set; }

    public bool Supprnuisance { get; set; }

    public virtual SousCategorie CodesouscatNavigation { get; set; } = null!;

    public virtual ICollection<Composer> Composers { get; set; } = new List<Composer>();
}
