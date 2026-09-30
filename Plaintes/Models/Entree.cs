using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Entree
{
    public int Identree { get; set; }

    public string Numentree { get; set; } = null!;

    public int Coderue { get; set; }

    public int Idbat { get; set; }

    public string? Numrue { get; set; }

    public virtual Rue CoderueNavigation { get; set; } = null!;

    public virtual Batiment IdbatNavigation { get; set; } = null!;
}
