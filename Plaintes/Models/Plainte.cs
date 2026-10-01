using System;
using System.Collections.Generic;

namespace Plaintes.Models;

public partial class Plainte
{
    public int NumPlainte { get; set; }

    public int IdUser { get; set; }

    public string CodePartieCommune { get; set; } = null!;

    public string CodeCat { get; set; } = null!;

    public int NumCite { get; set; }

    public DateOnly? DateDeb { get; set; }

    public DateOnly? DateFin { get; set; }

    public string? NumReclamation { get; set; }

    public string? Commentaire { get; set; }

    public bool Suppr { get; set; }

    public string NumEntree { get; set; } = null!;

    public int CodeRue { get; set; }

    public string CodEtat { get; set; } = null!;

    public string? NumRue { get; set; }

    public DateTime DateCreation { get; set; }

    public DateTime DateModification { get; set; }

    public virtual Categorie CodeCatNavigation { get; set; } = null!;

    public virtual PartieCommune CodePartieCommuneNavigation { get; set; } = null!;

    public virtual Rue CodeRueNavigation { get; set; } = null!;

    public virtual Etat CodEtatNavigation { get; set; } = null!;

    public virtual Utilisateur IdUserNavigation { get; set; } = null!;

    public virtual ICollection<Composer> Composers { get; set; } = new List<Composer>();

    public virtual Cite NumCiteNavigation { get; set; } = null!;

    public virtual ICollection<PieceJointe> PieceJointes { get; set; } = new List<PieceJointe>();
}

public class PlainteFiltres
{
    public int? numPlainte { get; set; }
    public string? cite { get; set; }
    public string? rue { get; set; }
    public string? ville { get; set; }
    public string? entree { get; set; }
    public string? reclamation { get; set; }
    public string? categorie { get; set; }
    public string? sousCategorie { get; set; }
    public string? nuisance { get; set; }
    public int? coutMin { get; set; }
    public int? coutMax { get; set; }
}
