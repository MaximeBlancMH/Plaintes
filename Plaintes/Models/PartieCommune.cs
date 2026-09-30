using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class PartieCommune
{
    public string Codepartiecommune { get; set; } = null!;

    public string? Libpartiecommune { get; set; }

    public bool Supprpc { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
