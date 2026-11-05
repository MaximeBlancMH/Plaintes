using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services;
using Xunit;

namespace Plaintes.Tests
{
    public class PurgePlaintesServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx = TestDb.Creer();
        private static readonly DateOnly Aujourdhui = new(2026, 10, 23);

        public void Dispose() => _ctx.Dispose();

        private void AjouterPlainte(int num, DateOnly debut, bool suppr = false, params bool[] piecesSupprimees)
        {
            _ctx.Add(new Plainte { NumPlainte = num, NumCite = 1, CodeCat = "C", CodePartieCommune = "A", CodeRue = 1, CodEtat = "Ec", NumReclamation = "1", DateDeb = debut, Suppr = suppr });
            for (int i = 0; i < piecesSupprimees.Length; i++)
                _ctx.Add(new PieceJointe { NumPiece = num * 10 + i, NumPlainte = num, TypePiece = "pdf", TaillePiece = "1", SupprPj = piecesSupprimees[i], CheminPiece = "x.pdf" });
        }

        [Fact]
        public async Task SupprimeLesPlaintesDe5AnsPile_EtPlus_MaisPasLaVeille()
        {
            AjouterPlainte(1, new DateOnly(2021, 10, 23));      // 5 ans pile
            AjouterPlainte(2, new DateOnly(2019, 1, 1));        // plus de 5 ans
            AjouterPlainte(3, new DateOnly(2021, 10, 24));      // 5 ans demain
            await _ctx.SaveChangesAsync();

            var (plaintes, _) = await PurgePlaintesService.SupprimerAnciennesAsync(_ctx, Aujourdhui);

            Assert.Equal(2, plaintes);
            Assert.True((await _ctx.Plaintes.FindAsync(1))!.Suppr);
            Assert.True((await _ctx.Plaintes.FindAsync(2))!.Suppr);
            Assert.False((await _ctx.Plaintes.FindAsync(3))!.Suppr);
        }

        [Fact]
        public async Task SupprimeLogiquementLesPiecesJointes_SansToucherAuxFichiersNiAuxPlaintesRecentes()
        {
            AjouterPlainte(1, new DateOnly(2020, 5, 1), false, false, false, true);
            AjouterPlainte(2, new DateOnly(2025, 5, 1), false, false);
            await _ctx.SaveChangesAsync();

            var (_, pieces) = await PurgePlaintesService.SupprimerAnciennesAsync(_ctx, Aujourdhui);

            Assert.Equal(2, pieces);                            // la pièce déjà supprimée n'est pas recomptée
            Assert.All(_ctx.PieceJointes.Where(pj => pj.NumPlainte == 1), pj => Assert.True(pj.SupprPj));
            Assert.All(_ctx.PieceJointes.Where(pj => pj.NumPlainte == 1), pj => Assert.Equal("x.pdf", pj.CheminPiece));
            Assert.False(_ctx.PieceJointes.Single(pj => pj.NumPlainte == 2).SupprPj);
        }

        [Fact]
        public async Task PlainteDejaSupprimee_NestPasRecomptee_MaisSesPiecesLeSont()
        {
            AjouterPlainte(1, new DateOnly(2020, 5, 1), true, false);
            await _ctx.SaveChangesAsync();

            Assert.Equal((0, 1), await PurgePlaintesService.SupprimerAnciennesAsync(_ctx, Aujourdhui));
            Assert.Equal((0, 0), await PurgePlaintesService.SupprimerAnciennesAsync(_ctx, Aujourdhui));     // second passage : rien à faire
        }

        [Fact]
        public async Task TraiteParLots_AuDelaDe500Plaintes()
        {
            for (int i = 1; i <= 1203; i++)
                AjouterPlainte(i, new DateOnly(2018, 1, 1));
            await _ctx.SaveChangesAsync();

            var (plaintes, _) = await PurgePlaintesService.SupprimerAnciennesAsync(_ctx, Aujourdhui);

            Assert.Equal(1203, plaintes);
            Assert.All(_ctx.Plaintes, p => Assert.True(p.Suppr));
        }

        [Theory]
        [InlineData("2026-10-23T21:30:00Z", 30)]            // 23h30 à Paris : minuit dans 30 min
        [InlineData("2026-10-24T22:00:00Z", 25 * 60)]       // minuit le 25 octobre, jour du passage à l'heure d'hiver : 25 h jusqu'au suivant
        [InlineData("2026-03-28T23:00:00Z", 23 * 60)]       // minuit le 29 mars, jour du passage à l'heure d'été : 23 h jusqu'au suivant
        public void DelaiJusquAMinuit_TombeJusteApresMinuitHeureDeParis(string maintenant, int minutes)
        {
            var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

            var delai = PurgePlaintesService.DelaiJusquAMinuit(DateTime.Parse(maintenant).ToUniversalTime(), paris);

            Assert.Equal(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(1), delai);
        }
    }
}
