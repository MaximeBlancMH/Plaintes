using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;

namespace Plaintes.Tests
{
    /// <summary>
    /// Crée une base en mémoire isolée (une par test) avec un jeu de données commun.
    /// </summary>
    internal static class TestDb
    {
        public static AppDbContext Creer()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        /// <summary>
        /// Jeu de données :
        /// - Plainte 1 : cité "Les Pins", catégorie C1, coût 100 (+ un détail supprimé à 9999), créée le 01/01/2026
        /// - Plainte 2 : cité "Les Chênes", catégorie C2, coût 500, créée le 01/02/2026
        /// - Plainte 3 : cité "Les Pins", catégorie C1, sans détail, créée le 01/03/2026
        /// - Plainte 4 : supprimée
        /// </summary>
        public static AppDbContext CreerAvecDonnees()
        {
            var ctx = Creer();

            var user = new Utilisateur { IdUser = 1, NomUser = "Dupont", UserEmail = "dupont@test.fr" };
            var etat = new Etat { CodEtat = "E1", LibEtat = "Ouvert" };

            var c1 = new Categorie { CodeCat = "C1", LibCat = "Bruit" };
            var c2 = new Categorie { CodeCat = "C2", LibCat = "Odeur" };
            var s1 = new SousCategorie { CodeSousCat = "S1", CodeCat = "C1", LibSousCat = "Voisinage", CodeCatNavigation = c1 };
            var s2 = new SousCategorie { CodeSousCat = "S2", CodeCat = "C2", LibSousCat = "Poubelles", CodeCatNavigation = c2 };
            var n1 = new Nuisance { CodeNuisance = "N1", CodeSousCat = "S1", LibNuisance = "Musique", CodeSousCatNavigation = s1 };
            var n2 = new Nuisance { CodeNuisance = "N2", CodeSousCat = "S2", LibNuisance = "Déchets", CodeSousCatNavigation = s2 };

            var pins = new Cite { NumCite = 1, LibCite = "Les Pins" };
            var chenes = new Cite { NumCite = 2, LibCite = "Les Chênes" };
            var rueA = new Rue { CodeRue = 1, LibRue = "Rue des Lilas" };
            var rueB = new Rue { CodeRue = 2, LibRue = "Rue du Moulin" };
            var hall = new PartieCommune { CodePartieCommune = "PC1", LibPartieCommune = "Hall" };
            var cave = new PartieCommune { CodePartieCommune = "PC2", LibPartieCommune = "Cave" };

            Plainte NouvellePlainte(int num, Cite cite, Rue rue, PartieCommune pc, Categorie cat, string entree, string reclamation, DateTime creation, bool suppr = false) => new()
            {
                NumPlainte = num,
                IdUser = user.IdUser,
                IdUserNavigation = user,
                NumCite = cite.NumCite,
                NumCiteNavigation = cite,
                CodeRue = rue.CodeRue,
                CodeRueNavigation = rue,
                CodePartieCommune = pc.CodePartieCommune,
                CodePartieCommuneNavigation = pc,
                CodeCat = cat.CodeCat,
                CodeCatNavigation = cat,
                CodEtat = etat.CodEtat,
                CodEtatNavigation = etat,
                NumEntree = entree,
                NumReclamation = reclamation,
                DateCreation = creation,
                DateModification = creation,
                Suppr = suppr,
            };

            var p1 = NouvellePlainte(1, pins, rueA, hall, c1, "E01", "R100", new DateTime(2026, 1, 1));
            var p2 = NouvellePlainte(2, chenes, rueB, cave, c2, "E02", "R200", new DateTime(2026, 2, 1));
            var p3 = NouvellePlainte(3, pins, rueA, hall, c1, "E03", "R300", new DateTime(2026, 3, 1));
            var p4 = NouvellePlainte(4, pins, rueA, hall, c1, "E04", "R400", new DateTime(2026, 4, 1), suppr: true);

            ctx.AddRange(user, etat, c1, c2, s1, s2, n1, n2, pins, chenes, rueA, rueB, hall, cave, p1, p2, p3, p4);
            ctx.AddRange(
                new Composer { NumDetail = 1, NumPlainte = 1, CodeNuisance = "N1", CodeNuisanceNavigation = n1, NumPlainteNavigation = p1, Cout = 100 },
                new Composer { NumDetail = 2, NumPlainte = 1, CodeNuisance = "N1", CodeNuisanceNavigation = n1, NumPlainteNavigation = p1, Cout = 9999, SupprDet = true },
                new Composer { NumDetail = 3, NumPlainte = 2, CodeNuisance = "N2", CodeNuisanceNavigation = n2, NumPlainteNavigation = p2, Cout = 500 });

            ctx.SaveChanges();
            ctx.ChangeTracker.Clear(); // les services testÃ©s relisent la base, sans entitÃ©s dÃ©jÃ  suivies
            return ctx;
        }
    }
}
