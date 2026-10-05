using System;
using System.Collections.Generic;

namespace Plaintes.Models;

/// <summary>Adresse d'un bâtiment : une rue et le numéro dans cette rue.</summary>
public partial class BatimentRue
{
    public int IdBat { get; set; }

    public int CodeRue { get; set; }

    public string NumRue { get; set; } = null!;

    public virtual Batiment IdBatNavigation { get; set; } = null!;

    public virtual Rue CodeRueNavigation { get; set; } = null!;
}
