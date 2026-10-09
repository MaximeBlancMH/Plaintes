namespace Plaintes.Models
{
    /// <summary>
    /// Une ligne d'un classement : un libellé et le nombre de dossiers associés
    /// </summary>
    public record StatLigne(string Libelle, int Valeur);

    public class StatistiquesViewModel
    {
        /// <summary>Année choisie, null = toutes les années</summary>
        public int? Annee { get; set; }
        public List<int> Annees { get; set; } = new();

        public int Total { get; set; }
        public int EnCours { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal CoutMoyen => Total == 0 ? 0 : CoutTotal / Total;

        /// <summary>Dossiers par mois (année choisie) ou par année (toutes les années)</summary>
        public List<StatLigne> Evolution { get; set; } = new();
        /// <summary>Même évolution pour l'année précédente (vide si toutes les années)</summary>
        public List<StatLigne> EvolutionPrecedente { get; set; } = new();
        public List<StatLigne> Secteurs { get; set; } = new();
        public List<StatLigne> Cites { get; set; } = new();
        public List<StatLigne> Categories { get; set; } = new();
        public List<StatLigne> Nuisances { get; set; } = new();
    }
}
