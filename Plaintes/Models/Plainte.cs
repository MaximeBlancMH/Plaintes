using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Plainte
{
    public int Numplainte { get; set; }

    public string Codeuser { get; set; } = null!;

    public string Codepartiecommune { get; set; } = null!;

    public string Codecat { get; set; } = null!;

    public int Numcite { get; set; }

    public DateOnly? Datedeb { get; set; }

    public DateOnly? Datefin { get; set; }

    public string? Numreclamation { get; set; }

    public string? Commentaire { get; set; }

    public bool Suppr { get; set; }

    public string Numentree { get; set; } = null!;

    public int Coderue { get; set; }

    public string Codetat { get; set; } = null!;

    public string? Numrue { get; set; }

    public virtual Categorie CodecatNavigation { get; set; } = null!;

    public virtual PartieCommune CodepartiecommuneNavigation { get; set; } = null!;

    public virtual Rue CoderueNavigation { get; set; } = null!;

    public virtual Etat CodetatNavigation { get; set; } = null!;

    public virtual Utilisateur CodeuserNavigation { get; set; } = null!;

    public virtual ICollection<Composer> Composers { get; set; } = new List<Composer>();

    public virtual Cite NumciteNavigation { get; set; } = null!;

    public virtual ICollection<PieceJointe> PieceJointes { get; set; } = new List<PieceJointe>();
}
