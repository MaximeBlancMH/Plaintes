namespace Plaintes.Models
{
    public class GestionTablesViewModel
    {
        public List<Cite> Cites { get; set; } = new();
        public List<CiteRue> CiteRues { get; set; } = new();
        public List<Rue> Rues { get; set; } = new();
        public List<Batiment> Batiments { get; set; } = new();
        public List<BatimentRue> BatimentRues { get; set; } = new();
        public List<Categorie> Categories { get; set; } = new();
        public List<SousCategorie> SousCategories { get; set; } = new();
        public List<Nuisance> Nuisances { get; set; } = new();
        public List<PartieCommune> PartieCommunes { get; set; } = new();
    }
}
