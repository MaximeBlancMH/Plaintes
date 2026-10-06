using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Plaintes.Controllers;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Reflection;

namespace Plaintes.Tests
{
    /// <summary>Page « Gestion des tables » : ajout et suppression d'éléments (HomeController.addElement et deleteElement).</summary>
    public class GestionTablesTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();
        private readonly HomeController _controller;

        public GestionTablesTests()
        {
            var httpContext = new DefaultHttpContext();
            _controller = new HomeController(NullLogger<HomeController>.Instance, _ctx, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new AccountControllerTests.TempDataVide())
            };
        }

        public void Dispose() => _ctx.Dispose();

        private string? Erreur => _controller.TempData["Erreur"] as string;
        private string? Succes => _controller.TempData["Succes"] as string;

        private static FormCollection Form(params (string, string)[] champs) =>
            new(champs.ToDictionary(c => c.Item1, c => new Microsoft.Extensions.Primitives.StringValues(c.Item2)));

        private async Task<IActionResult> Ajouter(string cat, params (string, string)[] champs)
        {
            var resultat = await _controller.addElement(cat, Form(champs));
            _ctx.ChangeTracker.Clear();     // on relit la base, sans entités déjà suivies
            return resultat;
        }

        private async Task Supprimer(string cat, string code)
        {
            var resultat = await _controller.deleteElement(cat, code);
            Assert.Equal("GestionTables", Assert.IsType<RedirectToActionResult>(resultat).ActionName);
            _ctx.ChangeTracker.Clear();
        }

        private async Task Sauver(params object[] entites)
        {
            _ctx.AddRange(entites);
            await _ctx.SaveChangesAsync();
            _ctx.ChangeTracker.Clear();
        }

        // ---------- Sécurité ----------

        [Theory]
        [InlineData(nameof(HomeController.addElement))]
        [InlineData(nameof(HomeController.deleteElement))]
        public void AjoutEtSuppression_SontReservesAuxAdministrateurs_EnPost_AvecJeton(string action)
        {
            var methode = typeof(HomeController).GetMethod(action)!;

            Assert.NotNull(methode.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(methode.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal("A,S", methode.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        }

        [Fact]
        public void LaPage_EstReserveeAuxAdministrateurs() =>
            Assert.Equal("A,S", typeof(HomeController).GetMethod(nameof(HomeController.GestionTables))!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);

        // ---------- Suppression : cité ----------

        [Fact]
        public async Task Supprimer_CiteLibre_EstSupprimee_AvecSesLiensAvecLesRues()
        {
            await Sauver(new Cite { NumCite = 1, LibCite = "Les Pins" }, new Rue { CodeRue = 5 }, new CiteRue { NumCite = 1, CodeRue = 5 });

            await Supprimer("Cite", "1");

            Assert.Empty(_ctx.Cites);
            Assert.Empty(_ctx.CiteRues);
            Assert.Single(_ctx.Rues);          // la rue elle-même reste
            Assert.NotNull(Succes);
        }

        [Fact]
        public async Task Supprimer_CiteUtiliseeParUnePlainte_EstRefusee()
        {
            await Sauver(new Cite { NumCite = 1 }, new Plainte { NumPlainte = 1, NumCite = 1, NumReclamation = "1", CodePartieCommune = "A", CodeCat = "C", CodEtat = "Ec" });

            await Supprimer("Cite", "1");

            Assert.Single(_ctx.Cites);
            Assert.Contains("plaintes", Erreur);
        }

        [Fact]
        public async Task Supprimer_CiteAvecDesBatiments_EstRefusee()
        {
            await Sauver(new Cite { NumCite = 1 }, new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" });

            await Supprimer("Cite", "1");

            Assert.Single(_ctx.Cites);
            Assert.Contains("bâtiments", Erreur);
        }

        // ---------- Suppression : rue ----------

        [Fact]
        public async Task Supprimer_RueLibre_EstSupprimee_AvecSesLiensAvecLesCites()
        {
            await Sauver(new Cite { NumCite = 1 }, new Rue { CodeRue = 5 }, new CiteRue { NumCite = 1, CodeRue = 5 });

            await Supprimer("Rue", "5");

            Assert.Empty(_ctx.Rues);
            Assert.Empty(_ctx.CiteRues);
            Assert.Single(_ctx.Cites);
        }

        [Fact]
        public async Task Supprimer_RueUtiliseeParUnePlainte_EstRefusee()
        {
            await Sauver(new Rue { CodeRue = 5 }, new Plainte { NumPlainte = 1, CodeRue = 5, NumReclamation = "1", CodePartieCommune = "A", CodeCat = "C", CodEtat = "Ec" });

            await Supprimer("Rue", "5");

            Assert.Single(_ctx.Rues);
            Assert.Contains("plaintes", Erreur);
        }

        [Fact]
        public async Task Supprimer_RueAvecDesBatiments_EstRefusee()
        {
            await Sauver(new Rue { CodeRue = 5 }, new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" }, new BatimentRue { IdBat = 1, CodeRue = 5, NumRue = "12" });

            await Supprimer("Rue", "5");

            Assert.Single(_ctx.Rues);
            Assert.Contains("bâtiments", Erreur);
        }

        // ---------- Suppression : bâtiment ----------

        [Fact]
        public async Task Supprimer_Batiment_EstSupprime_AvecSesAdresses_SansToucherAuxAutres()
        {
            await Sauver(new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" }, new Batiment { IdBat = 2, NumCite = 1, NumBat = "B2" },
                new BatimentRue { IdBat = 1, CodeRue = 5, NumRue = "12" }, new BatimentRue { IdBat = 2, CodeRue = 5, NumRue = "14" });

            await Supprimer("Batiment", "1");

            Assert.Equal(new[] { 2 }, _ctx.Batiments.Select(b => b.IdBat));
            Assert.Equal(new[] { 2 }, _ctx.BatimentRues.Select(b => b.IdBat));
        }

        // ---------- Suppression : éléments marqués supprimés (suppression logique) ----------

        [Fact]
        public async Task Supprimer_PartieCommune_LaMarqueSupprimee_SansLaRetirerDeLaBase()
        {
            await Sauver(new PartieCommune { CodePartieCommune = "HALL" }, new PartieCommune { CodePartieCommune = "CAVE" });

            await Supprimer("PartieCommune", "HALL");

            Assert.Equal(2, _ctx.PartieCommunes.Count());
            Assert.True((await _ctx.PartieCommunes.FindAsync("HALL"))!.SupprPc);
            Assert.False((await _ctx.PartieCommunes.FindAsync("CAVE"))!.SupprPc);
        }

        [Fact]
        public async Task Supprimer_Categorie_SansSousCategorie_EstMarqueeSupprimee()
        {
            await Sauver(new Categorie { CodeCat = "BRU" });

            await Supprimer("Categorie", "BRU");

            Assert.True((await _ctx.Categories.FindAsync("BRU"))!.SupprCat);
        }

        [Fact]
        public async Task Supprimer_CategorieAvecSousCategorieActive_EstRefusee_MaisPasSiElleEstDejaSupprimee()
        {
            await Sauver(new Categorie { CodeCat = "BRU" }, new SousCategorie { CodeSousCat = "VOIS", CodeCat = "BRU" });

            await Supprimer("Categorie", "BRU");
            Assert.False((await _ctx.Categories.FindAsync("BRU"))!.SupprCat);
            Assert.Contains("sous-catégories", Erreur);

            var sous = await _ctx.SousCategories.FindAsync("VOIS");
            sous!.SupprSousCat = true;
            await _ctx.SaveChangesAsync();
            _ctx.ChangeTracker.Clear();

            await Supprimer("Categorie", "BRU");
            Assert.True((await _ctx.Categories.FindAsync("BRU"))!.SupprCat);
        }

        [Fact]
        public async Task Supprimer_SousCategorieAvecNuisanceActive_EstRefusee()
        {
            await Sauver(new SousCategorie { CodeSousCat = "VOIS", CodeCat = "BRU" }, new Nuisance { CodeNuisance = "MUS", CodeSousCat = "VOIS" });

            await Supprimer("SousCategorie", "VOIS");

            Assert.False((await _ctx.SousCategories.FindAsync("VOIS"))!.SupprSousCat);
            Assert.Contains("nuisances", Erreur);
        }

        [Fact]
        public async Task Supprimer_Nuisance_EstMarqueeSupprimee()
        {
            await Sauver(new Nuisance { CodeNuisance = "MUS", CodeSousCat = "VOIS" });

            await Supprimer("Nuisance", "MUS");

            Assert.True((await _ctx.Nuisances.FindAsync("MUS"))!.SupprNuisance);
        }

        // ---------- Suppression demandée par la page (requête JavaScript) : réponse en JSON, sans rechargement ----------

        private async Task<(bool Ok, string Message)> SupprimerParLaPage(string cat, string code)
        {
            _controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            var json = Assert.IsType<JsonResult>(await _controller.deleteElement(cat, code));
            _ctx.ChangeTracker.Clear();
            var valeur = json.Value!;
            return ((bool)valeur.GetType().GetProperty("ok")!.GetValue(valeur)!, (string)valeur.GetType().GetProperty("message")!.GetValue(valeur)!);
        }

        [Fact]
        public async Task SupprimerParLaPage_Reussite_RenvoieUnJson_SansPasserParTempData()
        {
            await Sauver(new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" });

            var (ok, message) = await SupprimerParLaPage("Batiment", "1");

            Assert.True(ok);
            Assert.Equal("Élément supprimé.", message);
            Assert.Empty(_ctx.Batiments);
            Assert.Null(Succes);            // pas de message en attente : la page l'affiche elle-même, sans recharger
        }

        [Fact]
        public async Task SupprimerParLaPage_Refus_RenvoieLeMessageDansLeJson_EtNeSupprimeRien()
        {
            await Sauver(new Cite { NumCite = 1 }, new Batiment { IdBat = 1, NumCite = 1, NumBat = "B1" });

            var (ok, message) = await SupprimerParLaPage("Cite", "1");

            Assert.False(ok);
            Assert.Contains("bâtiments", message);
            Assert.Single(_ctx.Cites);
            Assert.Null(Erreur);
        }

        [Fact]
        public async Task Supprimer_SansEnteteJavaScript_RedirigeVersLaPage()
        {
            // Sans l'en-tête de la requête JavaScript (formulaire classique), le comportement reste la redirection.
            await Sauver(new Nuisance { CodeNuisance = "MUS", CodeSousCat = "VOIS" });

            await Supprimer("Nuisance", "MUS");

            Assert.NotNull(Succes);
        }

        // ---------- Suppression : cas d'erreur ----------

        [Theory]
        [InlineData("Cite", "99")]
        [InlineData("Rue", "99")]
        [InlineData("Batiment", "99")]
        [InlineData("PartieCommune", "ZZZZ")]
        [InlineData("Categorie", "ZZZZ")]
        [InlineData("SousCategorie", "ZZZZZ")]
        [InlineData("Nuisance", "ZZZZZ")]
        public async Task Supprimer_ElementInexistant_AfficheUneErreur(string cat, string code)
        {
            await Supprimer(cat, code);

            Assert.Equal("Élément introuvable.", Erreur);
            Assert.Null(Succes);
        }

        [Theory]
        [InlineData("Cite")]
        [InlineData("Rue")]
        [InlineData("Batiment")]
        public async Task Supprimer_IdentifiantNonNumerique_AfficheUneErreur(string cat)
        {
            await Supprimer(cat, "abc");

            Assert.Equal("Identifiant invalide.", Erreur);
        }

        [Fact]
        public async Task Supprimer_TypeInconnu_AfficheUneErreur()
        {
            await Supprimer("Truc", "1");

            Assert.Equal("Type d'élément inconnu.", Erreur);
        }

        [Fact]
        public async Task Supprimer_DeuxFois_LaSecondeFoisEstUneErreur()
        {
            await Sauver(new Nuisance { CodeNuisance = "MUS", CodeSousCat = "VOIS" });

            await Supprimer("Nuisance", "MUS");
            Assert.Null(Erreur);
            await Supprimer("Nuisance", "MUS");

            Assert.Equal("Élément introuvable.", Erreur);
        }

        // ---------- Ajout ----------

        [Fact]
        public async Task Ajouter_Cite_PrendLeNumeroSuivant_EtMetLeSecteurEnMajuscules()
        {
            await Sauver(new Cite { NumCite = 7, LibCite = "Existante" });

            var resultat = await Ajouter("Cite", ("LibCite", "Les Chênes"), ("Secteur", "sud"));

            Assert.Equal("GestionTables", Assert.IsType<RedirectToActionResult>(resultat).ActionName);
            var cite = await _ctx.Cites.SingleAsync(c => c.NumCite == 8);
            Assert.Equal(("Les Chênes", "SUD"), (cite.LibCite, cite.Secteur));
            Assert.NotNull(Succes);
        }

        [Fact]
        public async Task Ajouter_Cite_Premiere_ALeNumero1()
        {
            await Ajouter("Cite", ("LibCite", "Les Pins"), ("Secteur", "CENTRE"));

            Assert.Equal(1, (await _ctx.Cites.SingleAsync()).NumCite);
        }

        [Theory]
        [InlineData("", "SUD", "Libellé")]
        [InlineData("Les Pins", "", "Secteur")]
        [InlineData("Les Pins", "TROP LONG XX", "10 caractères")]
        public async Task Ajouter_Cite_ChampInvalide_AfficheUneErreur_SansRienAjouter(string libelle, string secteur, string attendu)
        {
            await Ajouter("Cite", ("LibCite", libelle), ("Secteur", secteur));

            Assert.Contains(attendu, Erreur);
            Assert.Empty(_ctx.Cites);
        }

        [Fact]
        public async Task Ajouter_Rue_EstRattacheeALaCiteChoisie()
        {
            await Sauver(new Cite { NumCite = 3 }, new Rue { CodeRue = 10 });

            await Ajouter("Rue", ("LibRue", "Rue neuve"), ("Cp", "71000"), ("Ville", "Mâcon"), ("NumCite", "3"));

            var rue = await _ctx.Rues.SingleAsync(r => r.CodeRue == 11);
            Assert.Equal(("Rue neuve", "71000", "Mâcon"), (rue.LibRue, rue.Cp, rue.Ville));
            var lien = await _ctx.CiteRues.SingleAsync();
            Assert.Equal((3, 11), (lien.NumCite, lien.CodeRue));
        }

        [Theory]
        [InlineData("99")]
        [InlineData("abc")]
        [InlineData("")]
        public async Task Ajouter_Rue_CiteInexistante_AfficheUneErreur(string numCite)
        {
            await Ajouter("Rue", ("LibRue", "Rue neuve"), ("Cp", "71000"), ("Ville", "Mâcon"), ("NumCite", numCite));

            Assert.Equal("La cité choisie n'existe pas.", Erreur);
            Assert.Empty(_ctx.Rues);
            Assert.Empty(_ctx.CiteRues);
        }

        [Fact]
        public async Task Ajouter_Batiment_EstEnregistreAvecSonAdresse()
        {
            await Sauver(new Cite { NumCite = 3 }, new Rue { CodeRue = 10 });

            await Ajouter("Batiment", ("NumCite", "3"), ("CodeRue", "10"), ("NumBat", "B1"), ("NumRue", "12"), ("LibBat", "Tour A"), ("CodeTranche", ""));

            var batiment = await _ctx.Batiments.SingleAsync();
            Assert.Equal((3, "B1", "Tour A", null), (batiment.NumCite, batiment.NumBat, batiment.LibBat, batiment.CodeTranche));
            var adresse = await _ctx.BatimentRues.SingleAsync();
            Assert.Equal((batiment.IdBat, 10, "12"), (adresse.IdBat, adresse.CodeRue, adresse.NumRue));
        }

        [Fact]
        public async Task Ajouter_Batiment_RueOuCiteInexistante_AfficheUneErreur()
        {
            await Sauver(new Cite { NumCite = 3 }, new Rue { CodeRue = 10 });

            await Ajouter("Batiment", ("NumCite", "99"), ("CodeRue", "10"), ("NumBat", "B1"), ("NumRue", "12"));
            Assert.Equal("La cité choisie n'existe pas.", Erreur);

            await Ajouter("Batiment", ("NumCite", "3"), ("CodeRue", "99"), ("NumBat", "B1"), ("NumRue", "12"));
            Assert.Equal("La rue choisie n'existe pas.", Erreur);
            Assert.Empty(_ctx.Batiments);
        }

        [Theory]
        [InlineData("", "12", "N° de bâtiment")]
        [InlineData("B1", "", "N° dans la rue")]
        public async Task Ajouter_Batiment_ChampObligatoireManquant_AfficheUneErreur(string numBat, string numRue, string attendu)
        {
            await Sauver(new Cite { NumCite = 3 }, new Rue { CodeRue = 10 });

            await Ajouter("Batiment", ("NumCite", "3"), ("CodeRue", "10"), ("NumBat", numBat), ("NumRue", numRue));

            Assert.Contains(attendu, Erreur);
            Assert.Empty(_ctx.Batiments);
        }

        [Fact]
        public async Task Ajouter_PartieCommune_MetLeCodeEnMajuscules_EtRefuseUnDoublon()
        {
            await Ajouter("PartieCommune", ("CodePartieCommune", "hall"), ("LibPartieCommune", "Hall d'entrée"));
            Assert.Equal("Hall d'entrée", (await _ctx.PartieCommunes.FindAsync("HALL"))!.LibPartieCommune);

            await Ajouter("PartieCommune", ("CodePartieCommune", "HALL"), ("LibPartieCommune", "Autre"));
            Assert.Equal("Ce code existe déjà.", Erreur);
            Assert.Single(_ctx.PartieCommunes);
        }

        [Fact]
        public async Task Ajouter_Categorie_RefuseUnCodeTropLongOuDejaPris()
        {
            await Ajouter("Categorie", ("CodeCat", "TROPLONG"), ("LibCat", "Bruit"));
            Assert.Contains("4 caractères", Erreur);

            await Ajouter("Categorie", ("CodeCat", "BRU"), ("LibCat", "Bruit"));
            Assert.Equal("BRU", (await _ctx.Categories.SingleAsync()).CodeCat);

            await Ajouter("Categorie", ("CodeCat", "bru"), ("LibCat", "Doublon"));
            Assert.Equal("Ce code existe déjà.", Erreur);
            Assert.Single(_ctx.Categories);
        }

        [Fact]
        public async Task Ajouter_SousCategorie_EstRattacheeALaCategorieChoisie()
        {
            await Sauver(new Categorie { CodeCat = "BRU" });

            await Ajouter("SousCategorie", ("CodeCat", "BRU"), ("CodeSousCat", "vois"), ("LibSousCat", "Voisinage"));

            var sous = await _ctx.SousCategories.SingleAsync();
            Assert.Equal(("VOIS", "BRU", "Voisinage"), (sous.CodeSousCat, sous.CodeCat, sous.LibSousCat));
        }

        [Fact]
        public async Task Ajouter_SousCategorie_CategorieInexistanteOuSupprimee_AfficheUneErreur()
        {
            await Sauver(new Categorie { CodeCat = "OLD", SupprCat = true });

            await Ajouter("SousCategorie", ("CodeCat", "ZZZ"), ("CodeSousCat", "VOIS"), ("LibSousCat", "Voisinage"));
            Assert.Equal("La catégorie choisie n'existe pas.", Erreur);

            await Ajouter("SousCategorie", ("CodeCat", "OLD"), ("CodeSousCat", "VOIS"), ("LibSousCat", "Voisinage"));
            Assert.Equal("La catégorie choisie n'existe pas.", Erreur);
            Assert.Empty(_ctx.SousCategories);
        }

        [Fact]
        public async Task Ajouter_Nuisance_EstRattacheeALaSousCategorieChoisie_EtRefuseUnDoublon()
        {
            await Sauver(new SousCategorie { CodeSousCat = "VOIS", CodeCat = "BRU" });

            await Ajouter("Nuisance", ("CodeSousCat", "VOIS"), ("CodeNuisance", "mus"), ("LibNuisance", "Musique"));
            var nuisance = await _ctx.Nuisances.SingleAsync();
            Assert.Equal(("MUS", "VOIS"), (nuisance.CodeNuisance, nuisance.CodeSousCat));

            await Ajouter("Nuisance", ("CodeSousCat", "VOIS"), ("CodeNuisance", "MUS"), ("LibNuisance", "Doublon"));
            Assert.Equal("Ce code existe déjà.", Erreur);
            Assert.Single(_ctx.Nuisances);
        }

        [Fact]
        public async Task Ajouter_Nuisance_SousCategorieInexistante_AfficheUneErreur()
        {
            await Ajouter("Nuisance", ("CodeSousCat", "ZZZZZ"), ("CodeNuisance", "MUS"), ("LibNuisance", "Musique"));

            Assert.Equal("La sous-catégorie choisie n'existe pas.", Erreur);
            Assert.Empty(_ctx.Nuisances);
        }

        [Fact]
        public async Task Ajouter_TypeInconnu_AfficheUneErreur()
        {
            await Ajouter("Truc");

            Assert.Equal("Type d'élément inconnu.", Erreur);
        }

        [Fact]
        public async Task Ajouter_EspacesAutourDesValeurs_SontRetires()
        {
            await Ajouter("Cite", ("LibCite", "  Les Pins  "), ("Secteur", " sud "));

            var cite = await _ctx.Cites.SingleAsync();
            Assert.Equal(("Les Pins", "SUD"), (cite.LibCite, cite.Secteur));
        }

        // ---------- Ajout demandé par la page (requête JavaScript) : réponse en JSON avec l'élément créé ----------

        private async Task<System.Text.Json.JsonElement> AjouterParLaPage(string cat, params (string, string)[] champs)
        {
            _controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            var json = Assert.IsType<JsonResult>(await _controller.addElement(cat, Form(champs)));
            _ctx.ChangeTracker.Clear();
            return System.Text.Json.JsonSerializer.SerializeToElement(json.Value);
        }

        [Fact]
        public async Task AjouterParLaPage_Cite_RenvoieLElementCree_SansPasserParTempData()
        {
            var reponse = await AjouterParLaPage("Cite", ("LibCite", "Les Pins"), ("Secteur", "sud"));

            Assert.True(reponse.GetProperty("ok").GetBoolean());
            Assert.Equal("Élément ajouté.", reponse.GetProperty("message").GetString());
            var element = reponse.GetProperty("element");
            Assert.Equal(1, element.GetProperty("id").GetInt32());
            Assert.Equal("Les Pins", element.GetProperty("lib").GetString());
            Assert.Equal("SUD", element.GetProperty("secteur").GetString());
            Assert.Null(Succes);
        }

        [Fact]
        public async Task AjouterParLaPage_Rue_RenvoieLaRueEtSaCite()
        {
            await Sauver(new Cite { NumCite = 3 });

            var element = (await AjouterParLaPage("Rue", ("LibRue", "Rue neuve"), ("Cp", "71000"), ("Ville", "Mâcon"), ("NumCite", "3"))).GetProperty("element");

            Assert.Equal(1, element.GetProperty("id").GetInt32());
            Assert.Equal(3, element.GetProperty("cite").GetInt32());
            Assert.Equal(("Rue neuve", "71000", "Mâcon"), (element.GetProperty("lib").GetString(), element.GetProperty("cp").GetString(), element.GetProperty("ville").GetString()));
        }

        [Fact]
        public async Task AjouterParLaPage_Batiment_RenvoieLeBatimentAvecSonIdentifiantEtSonAdresse()
        {
            await Sauver(new Cite { NumCite = 3 }, new Rue { CodeRue = 10 });

            var element = (await AjouterParLaPage("Batiment", ("NumCite", "3"), ("CodeRue", "10"), ("NumBat", "B1"), ("NumRue", "12"), ("LibBat", "Tour A"))).GetProperty("element");

            var enBase = await _ctx.Batiments.SingleAsync();
            Assert.Equal(enBase.IdBat, element.GetProperty("id").GetInt32());     // l'identifiant est celui généré à l'enregistrement
            Assert.Equal((3, 10, "B1", "12", "Tour A"),
                (element.GetProperty("cite").GetInt32(), element.GetProperty("rue").GetInt32(), element.GetProperty("num").GetString(),
                 element.GetProperty("numRue").GetString(), element.GetProperty("lib").GetString()));
        }

        [Fact]
        public async Task AjouterParLaPage_ElementsAvecCode_RenvoientLeurCodeEtLeurParent()
        {
            var pc = (await AjouterParLaPage("PartieCommune", ("CodePartieCommune", "hall"), ("LibPartieCommune", "Hall"))).GetProperty("element");
            Assert.Equal("HALL", pc.GetProperty("code").GetString());

            var cat = (await AjouterParLaPage("Categorie", ("CodeCat", "bru"), ("LibCat", "Bruit"))).GetProperty("element");
            Assert.Equal("BRU", cat.GetProperty("code").GetString());

            var sous = (await AjouterParLaPage("SousCategorie", ("CodeCat", "BRU"), ("CodeSousCat", "vois"), ("LibSousCat", "Voisinage"))).GetProperty("element");
            Assert.Equal(("VOIS", "BRU"), (sous.GetProperty("code").GetString(), sous.GetProperty("cat").GetString()));

            var nuis = (await AjouterParLaPage("Nuisance", ("CodeSousCat", "VOIS"), ("CodeNuisance", "mus"), ("LibNuisance", "Musique"))).GetProperty("element");
            Assert.Equal(("MUS", "VOIS"), (nuis.GetProperty("code").GetString(), nuis.GetProperty("sousCat").GetString()));
        }

        [Fact]
        public async Task AjouterParLaPage_Refus_RenvoieLeMessage_SansElement_EtNAjouteRien()
        {
            var reponse = await AjouterParLaPage("Cite", ("LibCite", ""), ("Secteur", "SUD"));

            Assert.False(reponse.GetProperty("ok").GetBoolean());
            Assert.Contains("Libellé", reponse.GetProperty("message").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, reponse.GetProperty("element").ValueKind);
            Assert.Empty(_ctx.Cites);
            Assert.Null(Erreur);
        }
    }
}
