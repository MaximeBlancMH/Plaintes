using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Dico
{
    public int Id { get; set; }

    public string Nomtable { get; set; } = null!;

    public string Champs { get; set; } = null!;

    public string Description { get; set; } = null!;
}
