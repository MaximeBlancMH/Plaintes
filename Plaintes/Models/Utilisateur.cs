using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Utilisateur
{
    public int IdUser { get; set; }

    public string? NomUser { get; set; }

    public string? Access { get; set; }

    public DateOnly? DateFinu { get; set; }

    public string? UserEmail { get; set; }

    public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
}
