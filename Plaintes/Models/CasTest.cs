namespace Plaintes.Models
{
    /// <summary>
    /// Un cas de test proposé sous une page par la zone de test (Views/Shared/_ZoneTest.cshtml, visible en développement uniquement).
    /// Trois façons de le lancer : un lien (GET), un formulaire (POST avec ses champs), ou un bouton qui déclenche un script de la page
    /// (<see cref="Script"/> : nom repris par l'attribut data-test du bouton).
    /// </summary>
    /// <param name="Groupe">Titre du groupe de boutons.</param>
    /// <param name="Libelle">Texte du bouton.</param>
    /// <param name="Attendu">Résultat attendu, affiché au survol du bouton.</param>
    /// <param name="Url">Adresse appelée (GET ou POST). Vide pour un cas lancé par script.</param>
    /// <param name="Methode">« GET » (lien) ou « POST » (formulaire avec jeton anti-falsification).</param>
    /// <param name="Champs">Champs envoyés avec un POST (nom, valeur).</param>
    /// <param name="Script">Nom du cas géré par le JavaScript de la page (attribut data-test).</param>
    public record CasTest(
        string Groupe,
        string Libelle,
        string Attendu,
        string? Url = null,
        string Methode = "GET",
        IReadOnlyList<(string Nom, string Valeur)>? Champs = null,
        string? Script = null);
}
