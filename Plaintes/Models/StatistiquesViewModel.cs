namespace Plaintes.Models
{
    /// <summary>
    /// Une ligne d'un classement : un libellé, une valeur et le code qui permet de filtrer en cliquant dessus (vide = pas de filtre possible)
    /// </summary>
    public record StatLigne(string Libelle, int Valeur, string? Code = null);

    /// <summary>
    /// Une case du calendrier : nombre de dossiers d'un mois d'une année
    /// </summary>
    public record CaseCalendrier(int Annee, int Mois, int Valeur);

    /// <summary>
    /// Une valeur proposée dans une liste de filtre. Parent : code de l'élément dont elle dépend (secteur d'une cité, catégorie d'une sous-catégorie...)
    /// </summary>
    public record OptionFiltre(string Code, string Libelle, string? Parent = null);

    /// <summary>
    /// Filtres de la page Statistiques (lus dans l'adresse : /Statistiques?debut=2025-01&amp;fin=2026-06&amp;secteur=SUD...)
    /// </summary>
    public class StatistiquesFiltres
    {
        /// <summary>Premier et dernier mois de la période (« aaaa-mm », date de début des faits) ; vide = sans limite</summary>
        public string? Debut { get; set; }
        public string? Fin { get; set; }
        public string? Secteur { get; set; }
        public int? Cite { get; set; }
        public string? Categorie { get; set; }
        public string? SousCategorie { get; set; }
        public string? Nuisance { get; set; }
        public string? PartieCommune { get; set; }
    }

    /// <summary>
    /// Résultat des calculs pour un jeu de filtres (renvoyé aussi en JSON quand on change un filtre)
    /// </summary>
    public class StatistiquesResultat
    {
        /// <summary>Bornes du curseur de période (« aaaa-mm ») : premier et dernier mois des dossiers qui répondent aux autres filtres</summary>
        public string? PeriodeMin { get; set; }
        public string? PeriodeMax { get; set; }
        /// <summary>Nombre de mois de la période affichée, et vrai si l'évolution est donnée mois par mois (24 mois ou moins)</summary>
        public int NbMois { get; set; }
        public bool Mensuel { get; set; }

        public int Total { get; set; }
        public int NbDetails { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal CoutMoyen => Total == 0 ? 0 : CoutTotal / Total;

        /// <summary>Dossiers et coûts par mois (année choisie) ou par année (toutes les années)</summary>
        public List<StatLigne> Evolution { get; set; } = new();
        public List<StatLigne> EvolutionCout { get; set; } = new();
        /// <summary>Mêmes courbes pour l'année précédente (vides si toutes les années)</summary>
        public List<StatLigne> EvolutionPrecedente { get; set; } = new();
        public List<StatLigne> EvolutionCoutPrecedente { get; set; } = new();

        public List<StatLigne> Secteurs { get; set; } = new();
        public List<StatLigne> Cites { get; set; } = new();
        public List<StatLigne> PartiesCommunes { get; set; } = new();
        /// <summary>Signalements (détails) par sous-catégorie</summary>
        public List<StatLigne> SousCategories { get; set; } = new();
        public List<StatLigne> Nuisances { get; set; } = new();
        public List<StatLigne> Rues { get; set; } = new();

        /// <summary>Dossiers par mois sur toutes les années (le filtre d'année n'est pas appliqué)</summary>
        public List<CaseCalendrier> Calendrier { get; set; } = new();

        // Statistiques de remplacement : la page les affiche à la place d'un graphique que les filtres rendent sans intérêt
        // (ex. les adresses à la place des cités quand une cité est choisie).
        /// <summary>Adresses (numéro et rue) les plus touchées</summary>
        public List<StatLigne> Adresses { get; set; } = new();
        /// <summary>Dossiers par mois de l'année (janvier à décembre), toutes années confondues</summary>
        public List<StatLigne> Saisonnalite { get; set; } = new();
        /// <summary>Dossiers selon leur nombre de pièces jointes</summary>
        public List<StatLigne> PiecesJointes { get; set; } = new();
        /// <summary>Coût total (en euros) par cité, par partie commune et par nuisance</summary>
        public List<StatLigne> CoutCites { get; set; } = new();
        public List<StatLigne> CoutPartiesCommunes { get; set; } = new();
        public List<StatLigne> CoutNuisances { get; set; } = new();
    }

    /// <summary>
    /// Valeurs proposées dans les listes de filtres
    /// </summary>
    public class StatistiquesOptions
    {
        public List<OptionFiltre> Secteurs { get; set; } = new();
        public List<OptionFiltre> Cites { get; set; } = new();
        public List<OptionFiltre> Categories { get; set; } = new();
        public List<OptionFiltre> SousCategories { get; set; } = new();
        public List<OptionFiltre> Nuisances { get; set; } = new();
        public List<OptionFiltre> PartiesCommunes { get; set; } = new();
    }

    public class StatistiquesPageViewModel
    {
        public StatistiquesFiltres Filtres { get; set; } = new();
        public StatistiquesResultat Resultat { get; set; } = new();
        public StatistiquesOptions Options { get; set; } = new();
    }
}
