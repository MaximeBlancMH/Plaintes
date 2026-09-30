using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Utilisateur
{
    public string Codeuser { get; set; } = null!;

    public string? Nomuser { get; set; }

    public string? Mdpuser { get; set; }

    public string? Access { get; set; }

    public bool Actif { get; set; }

    public DateOnly? Datefinu { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
