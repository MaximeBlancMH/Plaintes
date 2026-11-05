using Microsoft.EntityFrameworkCore;
using Plaintes.Data;

namespace Plaintes.Services
{
    /// <summary>
    /// Tâche de fond : chaque nuit à minuit (heure du serveur, Europe/Paris dans le conteneur), supprime logiquement les plaintes
    /// dont la date de début des faits a 5 ans ou plus, ainsi que leurs pièces jointes (les fichiers restent sur le disque).
    /// Elle passe aussi une fois au démarrage, pour rattraper une nuit où l'application était arrêtée.
    /// </summary>
    public class PurgePlaintesService : BackgroundService
    {
        /// <summary>Âge (en années, depuis la date de début des faits) à partir duquel une plainte est supprimée</summary>
        public const int DureeConservationAnnees = 5;
        // Nombre de lignes modifiées à chaque enregistrement (évite de tout charger en mémoire au premier passage)
        private const int TailleLot = 500;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PurgePlaintesService> _logger;

        public PurgePlaintesService(IServiceScopeFactory scopes, ILogger<PurgePlaintesService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                await PasserAsync(stop);
                try
                {
                    await Task.Delay(DelaiJusquAMinuit(DateTime.UtcNow, TimeZoneInfo.Local), stop);
                }
                catch (OperationCanceledException)
                {
                    return;     // arrêt de l'application
                }
            }
        }

        /// <summary>
        /// Fonction qui lance une suppression ; une erreur est journalisée sans arrêter la tâche (nouvel essai la nuit suivante)
        /// </summary>
        private async Task PasserAsync(CancellationToken stop)
        {
            try
            {
                // Le DbContext n'est pas un singleton : on en prend un neuf à chaque passage
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var (plaintes, pieces) = await SupprimerAnciennesAsync(db, DateOnly.FromDateTime(DateTime.Now), stop);
                _logger.LogInformation("Suppression automatique : {Plaintes} plainte(s) de {Annees} ans ou plus et {Pieces} pièce(s) jointe(s) supprimée(s)",
                    plaintes, DureeConservationAnnees, pieces);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de la suppression automatique des anciennes plaintes");
            }
        }

        /// <summary>
        /// Fonction qui supprime logiquement les plaintes dont la date de début des faits a 5 ans pile ou plus, et leurs pièces jointes
        /// (ex. le 23/10/2026 : plaintes commencées le 23/10/2021 ou avant)
        /// </summary>
        /// <param name="db">Contexte de base de données</param>
        /// <param name="aujourdhui">Date du jour</param>
        /// <returns>Nombre de plaintes et de pièces jointes supprimées</returns>
        public static async Task<(int Plaintes, int Pieces)> SupprimerAnciennesAsync(AppDbContext db, DateOnly aujourdhui, CancellationToken stop = default)
        {
            var limite = aujourdhui.AddYears(-DureeConservationAnnees);
            int nbPlaintes = 0, nbPieces = 0;

            while (true)
            {
                var lot = await db.Plaintes.Where(p => !p.Suppr && p.DateDeb <= limite).OrderBy(p => p.NumPlainte).Take(TailleLot).ToListAsync(stop);
                if (lot.Count == 0) break;
                lot.ForEach(p => p.Suppr = true);
                await db.SaveChangesAsync(stop);
                db.ChangeTracker.Clear();
                nbPlaintes += lot.Count;
            }

            // Pièces des plaintes trop anciennes, y compris celles déjà supprimées à la main avant (leurs pièces étaient restées actives)
            while (true)
            {
                var lot = await db.PieceJointes.Where(pj => !pj.SupprPj && pj.NumPlainteNavigation.DateDeb <= limite)
                    .OrderBy(pj => pj.NumPiece).Take(TailleLot).ToListAsync(stop);
                if (lot.Count == 0) break;
                lot.ForEach(pj => pj.SupprPj = true);
                await db.SaveChangesAsync(stop);
                db.ChangeTracker.Clear();
                nbPieces += lot.Count;
            }

            return (nbPlaintes, nbPieces);
        }

        /// <summary>
        /// Fonction qui calcule le temps restant jusqu'au prochain minuit dans le fuseau donné (tient compte des changements d'heure)
        /// </summary>
        /// <param name="maintenantUtc">Heure actuelle, en UTC</param>
        /// <param name="fuseau">Fuseau horaire dans lequel minuit est compté</param>
        public static TimeSpan DelaiJusquAMinuit(DateTime maintenantUtc, TimeZoneInfo fuseau)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(maintenantUtc, fuseau);
            var minuit = DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified);
            // Une seconde de marge : on se réveille sûrement après minuit, donc déjà sur la nouvelle date
            return TimeZoneInfo.ConvertTimeToUtc(minuit, fuseau) - maintenantUtc + TimeSpan.FromSeconds(1);
        }
    }
}
