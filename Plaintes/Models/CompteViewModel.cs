namespace Plaintes.Models
{
    /// <summary>Informations du compte connecté affichées sur la page "Mon Compte".</summary>
    public class CompteViewModel
    {
        public string? Nom { get; set; }
        public string? Email { get; set; }
        // Lettre telle que stockée dans Utilisateur.Access (R, W, A ou S).
        public string? Droit { get; set; }

        public string DroitLibelle => LibelleDroit(Droit);

        public static string LibelleDroit(string? lettre) => lettre?.Trim().ToUpperInvariant() switch
        {
            "R" => "Lecture",
            "W" => "Écriture",
            "A" => "Administrateur",
            "S" => "Super administrateur",
            _ => "Aucun droit"
        };
    }
}
