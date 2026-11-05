using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;
using Xunit;

namespace Plaintes.Tests
{
    public class StatistiquesSrvTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();

        public void Dispose() => _ctx.Dispose();

        private Task<StatistiquesResultat> Stats(StatistiquesFiltres? f = null) => new StatistiquesSrv(_ctx).GetStatistiques(f ?? new StatistiquesFiltres());

        /// <summary>
        /// Jeu de données :
        /// - Dossier 1 : déc. 2025, cité Pins (CENTRE), catégorie C1, partie commune Hall, fait Tag, rue 1, détail Musique (S1) à 100
        /// - Dossier 2 : janv. 2026, cité Pins, catégorie C1, Hall, sans fait, rue 1, détails Musique à 200 et Déchets (S2) à 50 (+ un supprimé à 9999)
        /// - Dossier 3 : janv. 2026, cité Chênes (SUD), catégorie C2, Cave, fait Tag, rue 2, détail Déchets sans coût
        /// - Dossier 4 : févr. 2026, supprimé
        /// </summary>
        private async Task Preparer()
        {
            _ctx.AddRange(
                new Cite { NumCite = 1, LibCite = "Les Pins", Secteur = "CENTRE" },
                new Cite { NumCite = 2, LibCite = "Les Chênes", Secteur = "sud " },
                new Rue { CodeRue = 1, LibRue = "Rue des Lilas" },
                new Rue { CodeRue = 2, LibRue = "Rue du Moulin" },
                new PartieCommune { CodePartieCommune = "PC1", LibPartieCommune = "Hall" },
                new PartieCommune { CodePartieCommune = "PC2", LibPartieCommune = "Cave" },
                new TypeFait { CodeFait = "TAG", LibFait = "Tag" },
                new Categorie { CodeCat = "C1", LibCat = "Bruit" },
                new Categorie { CodeCat = "C2", LibCat = "Propreté" },
                new SousCategorie { CodeSousCat = "S1", CodeCat = "C1", LibSousCat = "Voisinage" },
                new SousCategorie { CodeSousCat = "S2", CodeCat = "C2", LibSousCat = "Ordures" },
                new Nuisance { CodeNuisance = "N1", LibNuisance = "Musique", CodeSousCat = "S1" },
                new Nuisance { CodeNuisance = "N2", LibNuisance = "Déchets", CodeSousCat = "S2" });

            Plainte P(int num, int cite, string cat, string pc, string? fait, int rue, int annee, int mois, bool suppr = false) => new()
            {
                NumPlainte = num, NumCite = cite, CodeCat = cat, CodePartieCommune = pc, CodeFait = fait, CodeRue = rue,
                CodEtat = "Ec", Suppr = suppr, NumReclamation = num.ToString(), DateDeb = new DateOnly(annee, mois, 1)
            };
            _ctx.AddRange(
                P(1, 1, "C1", "PC1", "TAG", 1, 2025, 12),
                P(2, 1, "C1", "PC1", null, 1, 2026, 1),
                P(3, 2, "C2", "PC2", "TAG", 2, 2026, 1),
                P(4, 1, "C1", "PC1", null, 1, 2026, 2, suppr: true));
            _ctx.AddRange(
                new Composer { NumDetail = 1, NumPlainte = 1, CodeNuisance = "N1", Cout = 100 },
                new Composer { NumDetail = 2, NumPlainte = 2, CodeNuisance = "N1", Cout = 200 },
                new Composer { NumDetail = 3, NumPlainte = 2, CodeNuisance = "N2", Cout = 50 },
                new Composer { NumDetail = 4, NumPlainte = 2, CodeNuisance = "N1", Cout = 9999, SupprDet = true },
                new Composer { NumDetail = 5, NumPlainte = 3, CodeNuisance = "N2" },
                new Composer { NumDetail = 6, NumPlainte = 4, CodeNuisance = "N1", Cout = 5000 });
            await _ctx.SaveChangesAsync();
        }

        // ---------- Sans filtre ----------

        [Fact]
        public async Task SansFiltre_IgnoreLesDossiersEtDetailsSupprimes()
        {
            await Preparer();

            var r = await Stats();

            Assert.Equal(3, r.Total);
            Assert.Equal(4, r.NbDetails);
            Assert.Equal(350, r.CoutTotal);
            Assert.Equal(350m / 3, r.CoutMoyen);
            Assert.Equal(("2025-12", "2026-01"), (r.PeriodeMin, r.PeriodeMax));
        }

        [Fact]
        public async Task Classements_SontTriesEtPortentLeCodeDuFiltre()
        {
            await Preparer();

            var r = await Stats();

            Assert.Equal(new[] { ("Les Pins", 2, "1"), ("Les Chênes", 1, "2") }, r.Cites.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));
            Assert.Equal(new[] { ("CENTRE", 2), ("SUD", 1) }, r.Secteurs.Select(l => (l.Libelle, l.Valeur)));
            Assert.Equal(new[] { ("Hall", 2, "PC1"), ("Cave", 1, "PC2") }, r.PartiesCommunes.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));
            Assert.Equal(new[] { ("Ordures", 2, "S2"), ("Voisinage", 2, "S1") }, r.SousCategories.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));
            Assert.Equal(new[] { ("Musique", 2), ("Déchets", 2) }, r.Nuisances.Select(l => (l.Libelle, l.Valeur)).OrderByDescending(x => x.Libelle));
            Assert.Equal(new[] { ("Rue des Lilas", 2), ("Rue du Moulin", 1) }, r.Rues.Select(l => (l.Libelle, l.Valeur)));
        }

        /// <summary>Ajoute un dossier (cité Pins, catégorie C1) avec un détail Musique au coût donné</summary>
        private async Task AjouterDossier(int num, int annee, int mois, int? cout = null)
        {
            _ctx.Add(new Plainte { NumPlainte = num, NumCite = 1, CodeCat = "C1", CodePartieCommune = "PC1", CodeRue = 1, CodEtat = "Ec", NumReclamation = num.ToString(), DateDeb = new DateOnly(annee, mois, 1) });
            _ctx.Add(new Composer { NumDetail = 100 + num, NumPlainte = num, CodeNuisance = "N1", Cout = cout });
            await _ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task SansPeriode_CourteDuree_DonneDossiersEtCoutsMoisParMois_SansComparaison()
        {
            await Preparer();

            var r = await Stats();

            Assert.True(r.Mensuel);
            Assert.Equal(2, r.NbMois);
            Assert.Equal(new[] { ("déc. 25", 1), ("janv. 26", 2) }, r.Evolution.Select(l => (l.Libelle, l.Valeur)));
            Assert.Equal(new[] { ("déc. 25", 100), ("janv. 26", 250) }, r.EvolutionCout.Select(l => (l.Libelle, l.Valeur)));
            Assert.Empty(r.EvolutionPrecedente);          // pas de période choisie : pas de comparaison
        }

        [Fact]
        public async Task PlusDe24Mois_DonneLEvolutionAnneeParAnnee_SansTrou()
        {
            await Preparer();
            await AjouterDossier(10, 2023, 6, 40);

            var r = await Stats();

            Assert.False(r.Mensuel);
            Assert.Equal(new[] { ("2023", 1), ("2024", 0), ("2025", 1), ("2026", 2) }, r.Evolution.Select(l => (l.Libelle, l.Valeur)));
        }

        [Fact]
        public async Task Periode_NeGardeQueSesMois_EtLesCompareAUnAnAvant()
        {
            await Preparer();
            await AjouterDossier(10, 2025, 1, 70);

            var r = await Stats(new StatistiquesFiltres { Debut = "2025-12", Fin = "2026-01" });

            Assert.Equal(3, r.Total);
            Assert.Equal(new[] { 1, 2 }, r.Evolution.Select(l => l.Valeur));
            Assert.Equal(new[] { 0, 1 }, r.EvolutionPrecedente.Select(l => l.Valeur));       // déc. 2024, janv. 2025
            Assert.Equal(new[] { 0, 70 }, r.EvolutionCoutPrecedente.Select(l => l.Valeur));
            Assert.Equal(3, r.Calendrier.Count);          // le calendrier montre toujours toute la période
        }

        [Fact]
        public async Task Periode_UnSeulCote_EtMoisInvalide()
        {
            await Preparer();

            Assert.Equal(2, (await Stats(new StatistiquesFiltres { Debut = "2026-01" })).Total);
            Assert.Equal(1, (await Stats(new StatistiquesFiltres { Fin = "2025-12" })).Total);
            Assert.Equal(3, (await Stats(new StatistiquesFiltres { Debut = "abc", Fin = "2025-13" })).Total);
        }

        [Fact]
        public async Task BornesDuCurseur_SuiventLesAutresFiltres_MaisPasLaPeriode()
        {
            await Preparer();

            var tout = await Stats(new StatistiquesFiltres { Debut = "2026-01" });
            var chenes = await Stats(new StatistiquesFiltres { Cite = 2 });

            Assert.Equal(("2025-12", "2026-01"), (tout.PeriodeMin, tout.PeriodeMax));
            Assert.Equal(("2026-01", "2026-01"), (chenes.PeriodeMin, chenes.PeriodeMax));
        }

        [Fact]
        public async Task Calendrier_DonneLesDossiersParMois()
        {
            await Preparer();

            var r = await Stats();

            Assert.Equal(new[] { new CaseCalendrier(2025, 12, 1), new CaseCalendrier(2026, 1, 2) }, r.Calendrier);
        }

        // ---------- Filtres ----------

        [Fact]
        public async Task FiltreSecteur_IgnoreLaCasseEtLesEspaces()
        {
            await Preparer();

            var r = await Stats(new StatistiquesFiltres { Secteur = "SUD" });

            Assert.Equal(1, r.Total);
            Assert.Equal("Les Chênes", Assert.Single(r.Cites).Libelle);
        }

        [Fact]
        public async Task FiltreSecteurNonRenseigne_GardeLesCitesSansSecteur()
        {
            await Preparer();
            _ctx.AddRange(new Cite { NumCite = 3, LibCite = "Sans secteur" },
                new Plainte { NumPlainte = 10, NumCite = 3, CodeCat = "C1", CodePartieCommune = "PC1", CodeRue = 1, CodEtat = "Ec", NumReclamation = "10", DateDeb = new DateOnly(2026, 3, 1) });
            await _ctx.SaveChangesAsync();

            var r = await Stats(new StatistiquesFiltres { Secteur = StatistiquesSrv.NonRenseigne });

            Assert.Equal(1, r.Total);
            Assert.Equal("Sans secteur", Assert.Single(r.Cites).Libelle);
        }

        [Fact]
        public async Task FiltresCitePartieCommuneEtCategorie()
        {
            await Preparer();

            Assert.Equal(2, (await Stats(new StatistiquesFiltres { Cite = 1 })).Total);
            Assert.Equal(1, (await Stats(new StatistiquesFiltres { PartieCommune = "PC2" })).Total);
            Assert.Equal(2, (await Stats(new StatistiquesFiltres { Categorie = "C1" })).Total);
            Assert.Equal(1, (await Stats(new StatistiquesFiltres { Cite = 1, PartieCommune = "PC1", Categorie = "C1", Debut = "2026-01", Fin = "2026-12" })).Total);
        }

        [Fact]
        public async Task FiltreNuisance_GardeLesDossiersConcernesEtNeCompteQueSesDetails()
        {
            await Preparer();

            var r = await Stats(new StatistiquesFiltres { Nuisance = "N2" });

            Assert.Equal(2, r.Total);                 // dossiers 2 et 3
            Assert.Equal(2, r.NbDetails);             // seuls les détails « Déchets »
            Assert.Equal(50, r.CoutTotal);
            Assert.Equal("Déchets", Assert.Single(r.Nuisances).Libelle);
        }

        [Fact]
        public async Task FiltreSousCategorie_NeCompteQueSesNuisances()
        {
            await Preparer();

            var r = await Stats(new StatistiquesFiltres { SousCategorie = "S1" });

            Assert.Equal(2, r.Total);                 // dossiers 1 et 2
            Assert.Equal(300, r.CoutTotal);           // Musique : 100 + 200
            Assert.Equal("Musique", Assert.Single(r.Nuisances).Libelle);
        }

        [Fact]
        public async Task AucunDossier_TousLesCompteursSontAZero()
        {
            await Preparer();

            var r = await Stats(new StatistiquesFiltres { Cite = 999 });

            Assert.Equal(0, r.Total);
            Assert.Equal(0, r.CoutMoyen);
            Assert.Empty(r.Evolution);
            Assert.Empty(r.Cites);
            Assert.Null(r.PeriodeMin);                     // aucun dossier pour ces filtres : pas de curseur de période
        }

        [Fact]
        public async Task PartiesCommunes_AuDela6SontRegroupeesDansAutres_SansCode()
        {
            for (int i = 1; i <= 8; i++)
                _ctx.Add(new Plainte { NumPlainte = i, NumCite = 1, CodeCat = "C", CodePartieCommune = "P" + i, CodeRue = 1, CodEtat = "Ec", NumReclamation = "1", DateDeb = new DateOnly(2026, 1, 1) });
            await _ctx.SaveChangesAsync();

            var r = await Stats();

            Assert.Equal(7, r.PartiesCommunes.Count);
            Assert.Equal(("Autres", 2, (string?)null), (r.PartiesCommunes[6].Libelle, r.PartiesCommunes[6].Valeur, r.PartiesCommunes[6].Code));
        }

        [Fact]
        public async Task SousCategories_SuiventLaCategorieChoisie()
        {
            await Preparer();

            var r = await Stats(new StatistiquesFiltres { Categorie = "C2" });

            Assert.Equal(("Ordures", 1), (Assert.Single(r.SousCategories).Libelle, r.SousCategories[0].Valeur));
        }

        // ---------- Statistiques de remplacement ----------

        [Fact]
        public async Task CoutsParCitePartieCommuneEtNuisance_SontLesSommesDesDetails()
        {
            await Preparer();

            var r = await Stats();

            Assert.Equal(new[] { ("Les Pins", 350, "1") }, r.CoutCites.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));       // Chênes : aucun coût
            Assert.Equal(new[] { ("Hall", 350, "PC1") }, r.CoutPartiesCommunes.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));
            Assert.Equal(new[] { ("Musique", 300, "N1"), ("Déchets", 50, "N2") }, r.CoutNuisances.Select(l => (l.Libelle, l.Valeur, l.Code ?? "")));
        }

        [Fact]
        public async Task Adresses_ReunissentNumeroEtRue()
        {
            await Preparer();
            _ctx.Add(new Plainte { NumPlainte = 20, NumCite = 1, CodeCat = "C1", CodePartieCommune = "PC1", CodeRue = 1, NumRue = "12", CodEtat = "Ec", NumReclamation = "20", DateDeb = new DateOnly(2026, 2, 1) });
            await _ctx.SaveChangesAsync();

            var r = await Stats();

            Assert.Contains(r.Adresses, l => l.Libelle == "12 Rue des Lilas" && l.Valeur == 1);
            Assert.Contains(r.Adresses, l => l.Libelle == "Rue des Lilas" && l.Valeur == 2);   // sans numéro
        }

        [Fact]
        public async Task Saisonnalite_DonneLes12MoisToutesAnneesConfondues()
        {
            await Preparer();

            var r = await Stats();

            Assert.Equal(12, r.Saisonnalite.Count);
            Assert.Equal(("Janvier", 2), (r.Saisonnalite[0].Libelle, r.Saisonnalite[0].Valeur));
            Assert.Equal(("Décembre", 1), (r.Saisonnalite[11].Libelle, r.Saisonnalite[11].Valeur));
        }

        [Fact]
        public async Task PiecesJointes_ComptentLesDossiersParNombreDePieces_SansLesPiecesSupprimees()
        {
            await Preparer();
            PieceJointe Pj(int num, int plainte, bool suppr = false) => new() { NumPiece = num, NumPlainte = plainte, TypePiece = "pdf", TaillePiece = "1 Ko", SupprPj = suppr };
            _ctx.AddRange(Pj(1, 1), Pj(2, 2), Pj(3, 2), Pj(4, 2), Pj(5, 3, suppr: true));
            await _ctx.SaveChangesAsync();

            var r = await Stats();

            Assert.Equal(new[] { ("Aucune pièce", 1), ("1 pièce", 1), ("2 ou 3 pièces", 1) }, r.PiecesJointes.Select(l => (l.Libelle, l.Valeur)));
        }

        // ---------- Rappel des filtres (export Excel) ----------

        [Fact]
        public async Task DecrireFiltres_DonneLesLibellesPlutotQueLesCodes()
        {
            await Preparer();

            var rappel = await new StatistiquesSrv(_ctx).DecrireFiltres(new StatistiquesFiltres
            {
                Debut = "2025-03", Fin = "abc", Cite = 1, Categorie = "C1", Nuisance = "INCONNUE"
            });

            Assert.Equal(new[]
            {
                new FiltreExport("Début des faits, à partir de", "mars 2025"),      // « abc » n'est pas un mois : ignoré
                new FiltreExport("Cité", "Les Pins"),
                new FiltreExport("Catégorie", "Bruit"),
                new FiltreExport("Nuisance", "INCONNUE")                           // code absent des listes : affiché tel quel
            }, rappel);
            Assert.Empty(await new StatistiquesSrv(_ctx).DecrireFiltres(new StatistiquesFiltres()));
        }

        // ---------- Listes des filtres ----------

        [Fact]
        public async Task Options_DonnentLesListesAvecLeurParent()
        {
            await Preparer();

            var o = await new StatistiquesSrv(_ctx).GetOptions();

            Assert.Equal(new[] { "CENTRE", "SUD" }, o.Secteurs.Select(s => s.Code));
            Assert.Contains(o.Cites, c => c.Code == "2" && c.Parent == "SUD");
            Assert.Contains(o.SousCategories, s => s.Code == "S1" && s.Parent == "C1");
            Assert.Contains(o.Nuisances, n => n.Code == "N2" && n.Parent == "S2");
        }
    }
}
