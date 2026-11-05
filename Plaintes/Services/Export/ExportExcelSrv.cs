using ClosedXML.Excel;
using Plaintes.Models;
using System.Globalization;

namespace Plaintes.Services.Export
{
    public interface IExportExcelSrv
    {
        byte[] Dossiers(DossiersExport export);
        byte[] Statistiques(StatistiquesResultat resultat, List<FiltreExport> filtres);
    }

    /// <summary>
    /// Fabrique les classeurs Excel des pages Dossiers et Statistiques (ClosedXML). Les données arrivent déjà filtrées :
    /// ce service ne fait que les mettre en forme, une feuille par tableau, avec le rappel des filtres.
    /// Les textes sont écrits comme du texte (jamais comme des formules), même s'ils commencent par « = ».
    /// </summary>
    public class ExportExcelSrv : IExportExcelSrv
    {
        public const string TypeMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // Largeur maximale d'une colonne (en caractères) : un long commentaire ne doit pas faire une colonne immense
        private const double LargeurMax = 60;
        // Lignes lues pour calculer la largeur des colonnes (au-delà, le calcul serait long sans rien changer)
        private const int LignesPourLargeur = 300;
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        /// <summary>Montant en euros : écrit comme un nombre, affiché « 1 234 € »</summary>
        private record Euros(decimal Valeur);

        /// <summary>
        /// Fonction qui fabrique le classeur de la page Dossiers : une feuille « Dossiers » (une ligne par plainte),
        /// une feuille « Détails » (une ligne par nuisance signalée) et une feuille « Filtres »
        /// </summary>
        /// <param name="e">Plaintes filtrées et triées, nombre de pièces jointes et rappel des filtres</param>
        /// <returns>Le fichier .xlsx</returns>
        public byte[] Dossiers(DossiersExport e)
        {
            using var classeur = new XLWorkbook();

            var dossiers = classeur.AddWorksheet("Dossiers");
            Tableau(dossiers, 1, "Dossiers",
                new[] { "N°", "Saisi le", "Saisi par", "Secteur", "Cité", "Adresse", "Partie commune", "Début des faits", "Fin des faits",
                        "N° de réclamation", "Catégorie", "Type de faits", "Précision des faits", "État", "Nuisances", "Coût total", "Pièces jointes", "Commentaire" },
                e.Plaintes.Select(p => new object?[]
                {
                    p.NumPlainte,
                    p.DateCreation,
                    p.IdUserNavigation?.NomUser ?? p.IdUserNavigation?.UserEmail,
                    p.NumCiteNavigation?.Secteur,
                    p.NumCiteNavigation?.LibCite,
                    string.Join(" ", new[] { p.NumRue?.Trim(), p.CodeRueNavigation?.LibRue }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    p.CodePartieCommuneNavigation?.LibPartieCommune,
                    p.DateDeb,
                    p.DateFin,
                    p.NumReclamation,
                    p.CodeCatNavigation?.LibCat,
                    p.CodeFaitNavigation?.LibFait,
                    p.PrecisionFait,
                    p.CodEtatNavigation?.LibEtat,
                    string.Join(", ", p.Composers.Select(c => c.CodeNuisanceNavigation?.LibNuisance ?? c.CodeNuisance)),
                    p.Composers.Any(c => c.Cout.HasValue) ? new Euros(p.Composers.Sum(c => c.Cout ?? 0)) : null,
                    e.NbPieces.GetValueOrDefault(p.NumPlainte),
                    p.Commentaire
                }));
            dossiers.SheetView.FreezeRows(1);
            Ajuster(dossiers);

            var details = classeur.AddWorksheet("Détails");
            Tableau(details, 1, "Details",
                new[] { "N° du dossier", "Cité", "Début des faits", "Catégorie", "Sous-catégorie", "Nuisance", "Coût" },
                e.Plaintes.SelectMany(p => p.Composers.Select(c => new object?[]
                {
                    p.NumPlainte,
                    p.NumCiteNavigation?.LibCite,
                    p.DateDeb,
                    p.CodeCatNavigation?.LibCat,
                    c.CodeNuisanceNavigation?.CodeSousCatNavigation?.LibSousCat,
                    c.CodeNuisanceNavigation?.LibNuisance ?? c.CodeNuisance,
                    c.Cout.HasValue ? new Euros(c.Cout.Value) : null
                })));
            details.SheetView.FreezeRows(1);
            Ajuster(details);

            var filtres = classeur.AddWorksheet("Filtres");
            var ligne = Entete(filtres, "Export des dossiers", $"{e.Plaintes.Count} dossier{(e.Plaintes.Count > 1 ? "s" : "")}");
            TableauFiltres(filtres, ligne, e.Filtres);
            Ajuster(filtres);

            return Enregistrer(classeur);
        }

        /// <summary>
        /// Fonction qui fabrique le classeur de la page Statistiques : un résumé (filtres et chiffres clés), l'évolution,
        /// le calendrier, puis une feuille par classement (seulement ceux qui ont des valeurs)
        /// </summary>
        /// <param name="r">Statistiques calculées avec les filtres de la page</param>
        /// <param name="filtres">Rappel des filtres</param>
        /// <returns>Le fichier .xlsx</returns>
        public byte[] Statistiques(StatistiquesResultat r, List<FiltreExport> filtres)
        {
            using var classeur = new XLWorkbook();

            // ---------- Résumé ----------
            var resume = classeur.AddWorksheet("Résumé");
            var ligne = Entete(resume, "Statistiques des plaintes", null);
            ligne = TableauFiltres(resume, ligne, filtres);
            Tableau(resume, ligne, "ChiffresCles", new[] { "Chiffre clé", "Valeur" }, new[]
            {
                new object?[] { "Dossiers", r.Total },
                new object?[] { "Coût total", new Euros(r.CoutTotal) },
                new object?[] { "Coût moyen par dossier", new Euros(Math.Round(r.CoutMoyen)) },
                new object?[] { "Nuisances signalées", r.NbDetails }
            });
            Ajuster(resume);

            // ---------- Évolution (mois par mois ou année par année, comme sur la page) ----------
            var evolution = classeur.AddWorksheet("Évolution");
            var comparaison = r.EvolutionPrecedente.Count == r.Evolution.Count && r.Evolution.Count > 0;
            var entetes = new List<string> { r.Mensuel ? "Mois" : "Année", "Dossiers", "Coût" };
            if (comparaison) entetes.AddRange(new[] { "Dossiers un an avant", "Coût un an avant" });
            Tableau(evolution, 1, "Evolution", entetes.ToArray(), r.Evolution.Select((l, i) =>
            {
                var valeurs = new List<object?> { l.Libelle, l.Valeur, new Euros(ValeurA(r.EvolutionCout, i)) };
                if (comparaison) valeurs.AddRange(new object?[] { ValeurA(r.EvolutionPrecedente, i), new Euros(ValeurA(r.EvolutionCoutPrecedente, i)) });
                return valeurs.ToArray();
            }));
            evolution.SheetView.FreezeRows(1);
            Ajuster(evolution);

            // ---------- Calendrier : une ligne par année, une colonne par mois ----------
            var calendrier = classeur.AddWorksheet("Calendrier");
            Tableau(calendrier, 1, "Calendrier",
                new[] { "Année" }.Concat(Enumerable.Range(1, 12).Select(m => Fr.DateTimeFormat.GetAbbreviatedMonthName(m))).Append("Total").ToArray(),
                r.Calendrier.GroupBy(c => c.Annee).OrderBy(g => g.Key).Select(g => new object?[] { g.Key }
                    .Concat(Enumerable.Range(1, 12).Select(m => (object?)g.Where(c => c.Mois == m).Sum(c => c.Valeur)))
                    .Append(g.Sum(c => c.Valeur)).ToArray()));
            Ajuster(calendrier);

            // ---------- Classements ----------
            Classement(classeur, "Secteurs", "Secteurs", "Secteur", "Dossiers", r.Secteurs);
            Classement(classeur, "Cités", "Cites", "Cité", "Dossiers", r.Cites);
            Classement(classeur, "Rues", "Rues", "Rue", "Dossiers", r.Rues);
            Classement(classeur, "Adresses", "Adresses", "Adresse", "Dossiers", r.Adresses);
            Classement(classeur, "Parties communes", "PartiesCommunes", "Partie commune", "Dossiers", r.PartiesCommunes);
            Classement(classeur, "Sous-catégories", "SousCategories", "Sous-catégorie", "Signalements", r.SousCategories);
            Classement(classeur, "Nuisances", "Nuisances", "Nuisance", "Signalements", r.Nuisances);
            Classement(classeur, "Saisonnalité", "Saisonnalite", "Mois de l'année", "Dossiers", r.Saisonnalite);
            Classement(classeur, "Pièces jointes", "PiecesJointes", "Pièces jointes", "Dossiers", r.PiecesJointes);
            Classement(classeur, "Coût par cité", "CoutCites", "Cité", "Coût", r.CoutCites, euros: true);
            Classement(classeur, "Coût par partie commune", "CoutPartiesCommunes", "Partie commune", "Coût", r.CoutPartiesCommunes, euros: true);
            Classement(classeur, "Coût par nuisance", "CoutNuisances", "Nuisance", "Coût", r.CoutNuisances, euros: true);

            return Enregistrer(classeur);
        }

        /// <summary>
        /// Fonction qui ajoute une feuille avec un classement (libellé, valeur) ; rien si le classement est vide
        /// </summary>
        private static void Classement(XLWorkbook classeur, string feuille, string nomTableau, string colonne, string valeur, List<StatLigne> lignes, bool euros = false)
        {
            if (lignes.Count == 0) return;
            var ws = classeur.AddWorksheet(feuille);
            Tableau(ws, 1, nomTableau, new[] { colonne, valeur },
                lignes.Select(l => new object?[] { l.Libelle, euros ? new Euros(l.Valeur) : l.Valeur }));
            Ajuster(ws);
        }

        /// <summary>
        /// Fonction qui écrit le titre et la date de l'export en haut d'une feuille
        /// </summary>
        /// <returns>Première ligne libre (après une ligne vide)</returns>
        private static int Entete(IXLWorksheet ws, string titre, string? sousTitre)
        {
            ws.Cell(1, 1).Value = titre;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = $"Exporté le {DateTime.Now.ToString("dd/MM/yyyy 'à' HH:mm", Fr)}" + (sousTitre == null ? "" : " — " + sousTitre);
            ws.Cell(2, 1).Style.Font.Italic = true;
            return 4;
        }

        /// <summary>
        /// Fonction qui écrit le rappel des filtres (ou « Aucun filtre »)
        /// </summary>
        /// <returns>Première ligne libre (après une ligne vide)</returns>
        private static int TableauFiltres(IXLWorksheet ws, int ligne, List<FiltreExport> filtres)
        {
            if (filtres.Count == 0)
            {
                ws.Cell(ligne, 1).Value = "Aucun filtre : toutes les plaintes non supprimées";
                return ligne + 2;
            }
            return Tableau(ws, ligne, "Filtres", new[] { "Filtre", "Valeur" }, filtres.Select(f => new object?[] { f.Libelle, f.Valeur }));
        }

        /// <summary>
        /// Fonction qui écrit un tableau Excel (en-têtes, lignes, filtres automatiques et lignes alternées) à partir de la ligne donnée.
        /// Sans données, seuls les en-têtes sont écrits, suivis de « Aucune donnée ».
        /// </summary>
        /// <param name="nom">Nom du tableau dans Excel (unique dans le classeur, sans espace ni accent)</param>
        /// <returns>Première ligne libre (après une ligne vide)</returns>
        private static int Tableau(IXLWorksheet ws, int ligne, string nom, string[] entetes, IEnumerable<object?[]> lignes)
        {
            for (int c = 0; c < entetes.Length; c++)
                ws.Cell(ligne, c + 1).Value = entetes[c];

            int derniere = ligne;
            foreach (var valeurs in lignes)
            {
                derniere++;
                for (int c = 0; c < entetes.Length; c++)
                    Ecrire(ws.Cell(derniere, c + 1), valeurs[c]);
            }

            if (derniere == ligne)
            {
                ws.Range(ligne, 1, ligne, entetes.Length).Style.Font.Bold = true;
                ws.Cell(ligne + 1, 1).Value = "Aucune donnée";
                ws.Cell(ligne + 1, 1).Style.Font.Italic = true;
                return ligne + 3;
            }

            var tableau = ws.Range(ligne, 1, derniere, entetes.Length).CreateTable(nom);
            tableau.Theme = XLTableTheme.TableStyleMedium2;
            return derniere + 2;
        }

        /// <summary>
        /// Fonction qui écrit une valeur dans une cellule avec le bon type (nombre, date, texte) et le bon format d'affichage
        /// </summary>
        private static void Ecrire(IXLCell cellule, object? valeur)
        {
            switch (valeur)
            {
                case null:
                    break;
                case Euros e:
                    cellule.Value = (double)e.Valeur;
                    cellule.Style.NumberFormat.Format = "#,##0 \"€\"";
                    break;
                case int i:
                    cellule.Value = i;
                    break;
                case DateOnly d:
                    cellule.Value = d.ToDateTime(TimeOnly.MinValue);
                    cellule.Style.DateFormat.Format = "dd/mm/yyyy";
                    break;
                case DateTime dt:
                    cellule.Value = dt;
                    cellule.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                    break;
                default:
                    cellule.Value = valeur.ToString();     // toujours du texte, jamais une formule
                    break;
            }
        }

        /// <summary>
        /// Fonction qui adapte la largeur des colonnes à leur contenu, sans dépasser LargeurMax
        /// </summary>
        private static void Ajuster(IXLWorksheet ws)
        {
            foreach (var colonne in ws.ColumnsUsed())
            {
                colonne.AdjustToContents(1, LignesPourLargeur);
                if (colonne.Width > LargeurMax) colonne.Width = LargeurMax;
            }
        }

        /// <summary>Valeur de la i-ème ligne d'une série (0 si elle n'existe pas)</summary>
        private static int ValeurA(List<StatLigne> serie, int i) => i < serie.Count ? serie[i].Valeur : 0;

        private static byte[] Enregistrer(XLWorkbook classeur)
        {
            using var flux = new MemoryStream();
            classeur.SaveAs(flux);
            return flux.ToArray();
        }
    }
}
