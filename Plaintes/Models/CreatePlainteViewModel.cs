using System.ComponentModel.DataAnnotations;

namespace Plaintes.Models
{
    /// <summary>Données du formulaire de création d'une plainte (champs saisis + listes de choix).</summary>
    public class CreatePlainteViewModel
    {
        // N° de la plainte en cours de modification ; 0 pour une création. Renseigné par le contrôleur (jamais lu depuis le formulaire).
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public int NumPlainte { get; set; }
        public bool EstModification => NumPlainte > 0;

        // ---------- Champs saisis ----------
        [Display(Name = "N° de réclamation")]
        [Required(ErrorMessage = "Le n° de réclamation est obligatoire.")]
        public string? NumReclamation { get; set; }

        [Display(Name = "Type de faits")]
        public string? CodeFait { get; set; }

        [Display(Name = "Précision")]
        public string? PrecisionFait { get; set; }

        // Patrimoine
        [Display(Name = "Partie commune")]
        [Required(ErrorMessage = "Choisissez une partie commune.")]
        public string? CodePartieCommune { get; set; }

        [Display(Name = "Secteur")]
        [Required(ErrorMessage = "Choisissez un secteur.")]
        public string? Secteur { get; set; }

        [Display(Name = "Cité")]
        [Required(ErrorMessage = "Choisissez une cité.")]
        public int? NumCite { get; set; }

        [Display(Name = "Adresse")]
        [Required(ErrorMessage = "Choisissez une adresse.")]
        public int? CodeRue { get; set; }

        // Numéro dans la rue (batiment_rue.NUMRUE), choisi avec l'adresse.
        public string? NumRue { get; set; }

        // Période
        [Display(Name = "Date de début")]
        [Required(ErrorMessage = "La date de début est obligatoire.")]
        [DataType(DataType.Date)]
        public DateOnly? DateDeb { get; set; }

        [Display(Name = "Date de fin")]
        [DataType(DataType.Date)]
        public DateOnly? DateFin { get; set; }

        [Display(Name = "Commentaire")]
        public string? Commentaire { get; set; }

        [Display(Name = "Catégorie")]
        [Required(ErrorMessage = "Choisissez une catégorie.")]
        public string? CodeCat { get; set; }

        public List<DetailPlainteViewModel> Details { get; set; } = new();
        public List<PieceJointeViewModel> Pieces { get; set; } = new();

        // Modification : pièces déjà enregistrées (affichage) et numéros de celles que l'utilisateur retire.
        public List<PieceExistanteViewModel> PiecesExistantes { get; set; } = new();
        public List<int> PiecesASupprimer { get; set; } = new();

        // Message d'erreur affiché au-dessus du formulaire (rien à afficher si vide).
        public string? Erreur { get; set; }

        // ---------- Listes de choix (remplies par le contrôleur, non envoyées par le formulaire) ----------
        public List<TypeFait> TypesFait { get; set; } = new();
        public List<PartieCommune> PartiesCommunes { get; set; } = new();
        public List<string> Secteurs { get; set; } = new();
        public List<CiteChoix> Cites { get; set; } = new();
        public List<Categorie> Categories { get; set; } = new();
        public List<SousCategorieChoix> SousCategories { get; set; } = new();
        public List<NuisanceChoix> Nuisances { get; set; } = new();
        public List<NaturePiece> Natures { get; set; } = new();
    }

    /// <summary>Un détail de plainte : une nuisance (avec sa sous-catégorie) et son coût.</summary>
    public class DetailPlainteViewModel
    {
        public string? CodeSousCat { get; set; }
        public string? CodeNuisance { get; set; }
        public int? Cout { get; set; }
    }

    /// <summary>
    /// Une pièce jointe envoyée avec le formulaire. Le fichier n'est pas stocké en base : il est écrit sur disque
    /// (IPieceStorage) et seul son chemin est enregistré (piece_jointe.CHEMINPIECE).
    /// </summary>
    public class PieceJointeViewModel
    {
        [Required(ErrorMessage = "Le fichier d'une pièce jointe est obligatoire.")]
        public IFormFile? Fichier { get; set; }

        [Required(ErrorMessage = "La nature d'une pièce jointe est obligatoire.")]
        public string? CodeNature { get; set; }

        [StringLength(100, ErrorMessage = "La précision de la nature ne peut pas dépasser 100 caractères.")]
        public string? PrecisionNature { get; set; }

        /// <summary>
        /// Nom d'origine du fichier, réduit au seul nom (sans dossier). Les navigateurs peuvent envoyer un chemin complet
        /// avec des "\" (Windows) : sous Linux, Path.GetFileName ne coupe que sur "/", donc on coupe nous-mêmes sur les deux.
        /// Sert à piece_jointe.LIBPIECE (150 caractères) ; jamais utilisé pour construire un chemin sur le disque.
        /// </summary>
        public string? NomFichier
        {
            get
            {
                var nom = Fichier?.FileName;
                if (string.IsNullOrWhiteSpace(nom)) return null;
                nom = nom[(nom.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];
                nom = new string(nom.Where(c => !char.IsControl(c)).ToArray()).Trim();
                return nom.Length == 0 ? null : nom;
            }
        }

        /// <summary>Extension en minuscules, avec le point (".pdf"), comparée à Storage:Extensions.</summary>
        public string Extension => Path.GetExtension(NomFichier ?? "").ToLowerInvariant();
    }

    /// <summary>Une pièce déjà enregistrée d'une plainte, affichée dans le formulaire de modification (sans son contenu).</summary>
    public record PieceExistanteViewModel(int NumPiece, string Nom, string? Taille, string? Nature);

    // Versions allégées pour les listes dépendantes (sérialisées en JSON pour la vue).
    public record CiteChoix(int NumCite, string? LibCite, string? Secteur);
    public record SousCategorieChoix(string CodeSousCat, string CodeCat, string? LibSousCat);
    public record NuisanceChoix(string CodeNuisance, string CodeSousCat, string? LibNuisance);
}
