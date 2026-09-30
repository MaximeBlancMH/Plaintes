using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Etat
{
    public string Codetat { get; set; } = null!;

    public string Libetat { get; set; } = null!;

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
