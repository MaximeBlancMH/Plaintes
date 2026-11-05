using ClosedXML.Excel;
using Plaintes.Models;
using Plaintes.Services.Export;
using Xunit;

namespace Plaintes.Tests
{
    /// <summary>
    /// Vérifie le contenu des classeurs : on relit le fichier .xlsx produit avec ClosedXML.
    /// </summary>
    public class ExportExcelSrvTests
    {
        private readonly ExportExcelSrv _srv = new();

        private static XLWorkbook Lire(byte[] fichier) => new(new MemoryStream(fichier));

        private static DossiersExport UnDossier()
        {
            var sousCat = new SousCategorie { CodeSousCat = "S1", LibSousCat = "Voisinage" };
            var plainte = new Plainte
            {
                NumPlainte = 7, DateCreation = new DateTime(2026, 2, 3, 14, 30, 0), DateDeb = new DateOnly(2026, 1, 15), NumReclamation = "R7",
                NumRue = "12", Commentaire = "=1+1 pas une formule",
                IdUserNavigation = new Utilisateur { NomUser = "Dupont" },
                NumCiteNavigation = new Cite { LibCite = "Les Pins", Secteur = "CENTRE" },
                CodeRueNavigation = new Rue { LibRue = "Rue des Lilas" },
                CodePartieCommuneNavigation = new PartieCommune { LibPartieCommune = "Hall" },
                CodeCatNavigation = new Categorie { LibCat = "Bruit" },
                CodEtatNavigation = new Etat { LibEtat = "Ouvert" },
                Composers =
                {
                    new Composer { CodeNuisance = "N1", Cout = 100, CodeNuisanceNavigation = new Nuisance { LibNuisance = "Musique", CodeSousCatNavigation = sousCat } },
                    new Composer { CodeNuisance = "N2", Cout = null, CodeNuisanceNavigation = new Nuisance { LibNuisance = "Cris", CodeSousCatNavigation = sousCat } }
                }
            };
            return new DossiersExport(new() { plainte }, new() { [7] = 2 }, new() { new FiltreExport("Cité (contient)", "Pins") });
        }

        /// <summary>Valeurs d'une ligne, indexées par l'en-tête de la colonne (ligne 1)</summary>
        private static Dictionary<string, IXLCell> Ligne(IXLWorksheet ws, int ligne) =>
            ws.Row(1).CellsUsed().ToDictionary(c => c.GetString(), c => ws.Cell(ligne, c.Address.ColumnNumber));

        [Fact]
        public void Dossiers_UneLigneParPlainte_AvecLesBonsTypes()
        {
            using var classeur = Lire(_srv.Dossiers(UnDossier()));

            Assert.Equal(new[] { "Dossiers", "Détails", "Filtres" }, classeur.Worksheets.Select(w => w.Name));
            var l = Ligne(classeur.Worksheet("Dossiers"), 2);
            Assert.Equal(7, l["N°"].GetValue<int>());
            Assert.Equal(new DateTime(2026, 2, 3, 14, 30, 0), l["Saisi le"].GetDateTime());
            Assert.Equal(new DateTime(2026, 1, 15), l["Début des faits"].GetDateTime());
            Assert.True(l["Fin des faits"].IsEmpty());
            Assert.Equal("12 Rue des Lilas", l["Adresse"].GetString());
            Assert.Equal("Musique, Cris", l["Nuisances"].GetString());
            Assert.Equal(100, l["Coût total"].GetDouble());
            Assert.Equal(2, l["Pièces jointes"].GetValue<int>());
            Assert.Equal("Ouvert", l["État"].GetString());
            // Un texte qui commence par « = » reste du texte
            Assert.False(l["Commentaire"].HasFormula);
            Assert.Equal("=1+1 pas une formule", l["Commentaire"].GetString());
        }

        [Fact]
        public void Dossiers_UneLigneParNuisance_DansDetails_EtLeRappelDesFiltres()
        {
            using var classeur = Lire(_srv.Dossiers(UnDossier()));

            var details = classeur.Worksheet("Détails");
            Assert.Equal(new[] { "Musique", "Cris" }, new[] { Ligne(details, 2)["Nuisance"].GetString(), Ligne(details, 3)["Nuisance"].GetString() });
            Assert.Equal("Voisinage", Ligne(details, 2)["Sous-catégorie"].GetString());
            Assert.True(Ligne(details, 3)["Coût"].IsEmpty());     // détail sans coût : case vide, pas 0

            var filtres = classeur.Worksheet("Filtres");
            Assert.Contains("1 dossier", filtres.Cell(2, 1).GetString());
            Assert.Equal(("Cité (contient)", "Pins"), (filtres.Cell(5, 1).GetString(), filtres.Cell(5, 2).GetString()));
        }

        [Fact]
        public void Dossiers_SansResultatNiFiltre_DonneUnClasseurLisible()
        {
            using var classeur = Lire(_srv.Dossiers(new DossiersExport(new(), new(), new())));

            Assert.Equal("Aucune donnée", classeur.Worksheet("Dossiers").Cell(2, 1).GetString());
            Assert.StartsWith("Aucun filtre", classeur.Worksheet("Filtres").Cell(4, 1).GetString());
        }

        [Fact]
        public void Statistiques_ResumeEvolutionCalendrier_EtSeulementLesClassementsNonVides()
        {
            var r = new StatistiquesResultat
            {
                Total = 3, NbDetails = 4, CoutTotal = 250, Mensuel = true,
                Evolution = { new("déc. 25", 1), new("janv. 26", 2) },
                EvolutionCout = { new("déc. 25", 100), new("janv. 26", 150) },
                EvolutionPrecedente = { new("déc. 25", 0), new("janv. 26", 1) },
                EvolutionCoutPrecedente = { new("déc. 25", 0), new("janv. 26", 70) },
                Calendrier = { new(2025, 12, 1), new(2026, 1, 2) },
                Secteurs = { new("CENTRE", 2, "CENTRE"), new("SUD", 1, "SUD") },
                CoutCites = { new("Les Pins", 250, "1") }
            };

            using var classeur = Lire(_srv.Statistiques(r, new() { new FiltreExport("Secteur", "CENTRE") }));

            Assert.Equal(new[] { "Résumé", "Évolution", "Calendrier", "Secteurs", "Coût par cité" }, classeur.Worksheets.Select(w => w.Name));

            var resume = classeur.Worksheet("Résumé");
            Assert.Equal(("Secteur", "CENTRE"), (resume.Cell(5, 1).GetString(), resume.Cell(5, 2).GetString()));
            var chiffres = resume.Column(1).CellsUsed().ToDictionary(c => c.GetString(), c => c.CellRight());
            Assert.Equal(3, chiffres["Dossiers"].GetDouble());
            Assert.Equal(250, chiffres["Coût total"].GetDouble());
            Assert.Equal(83, chiffres["Coût moyen par dossier"].GetDouble());

            var evolution = Ligne(classeur.Worksheet("Évolution"), 3);
            Assert.Equal("janv. 26", evolution["Mois"].GetString());
            Assert.Equal((2, 150, 1, 70), (evolution["Dossiers"].GetValue<int>(), evolution["Coût"].GetValue<int>(),
                evolution["Dossiers un an avant"].GetValue<int>(), evolution["Coût un an avant"].GetValue<int>()));

            var annee2026 = Ligne(classeur.Worksheet("Calendrier"), 3);
            Assert.Equal((2026, 2, 0, 2), (annee2026["Année"].GetValue<int>(), annee2026["janv."].GetValue<int>(),
                annee2026["févr."].GetValue<int>(), annee2026["Total"].GetValue<int>()));
        }

        [Fact]
        public void Statistiques_SansComparaison_NAPasLesColonnesUnAnAvant()
        {
            var r = new StatistiquesResultat { Evolution = { new("2025", 4), new("2026", 2) }, EvolutionCout = { new("2025", 10), new("2026", 0) } };

            using var classeur = Lire(_srv.Statistiques(r, new()));

            Assert.Equal(new[] { "Année", "Dossiers", "Coût" }, classeur.Worksheet("Évolution").Row(1).CellsUsed().Select(c => c.GetString()));
            Assert.StartsWith("Aucun filtre", classeur.Worksheet("Résumé").Cell(4, 1).GetString());
        }
    }
}
