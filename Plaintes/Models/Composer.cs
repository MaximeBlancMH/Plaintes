using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Composer
{
    public int Numdetail { get; set; }

    public string Codenuisance { get; set; } = null!;

    public int Numplainte { get; set; }

    public int? Cout { get; set; }

    public bool Supprdet { get; set; }

    public virtual Nuisance CodenuisanceNavigation { get; set; } = null!;

    public virtual Plainte NumplainteNavigation { get; set; } = null!;
}
