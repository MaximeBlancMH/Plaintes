using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Plaintes.Data;
using Plaintes.Models;
using System.Security.Claims;

namespace Plaintes.Services
{
    /// <summary>
    /// Règles de connexion. Un compte « refusé » (inconnu de la base, ou supprimé) reçoit à la connexion le claim <see cref="Refuse"/> :
    /// il n'a alors accès à aucune page protégée et est envoyé vers la page « accès refusé ».
    /// </summary>
    public static class ComptesClaims
    {
        public const string Refuse = "compte_refuse";

        /// <summary>
        /// Fonction qui détermine si l'utilisateur est encore actif
        /// </summary>
        /// <param name="utilisateur">Utilisateur en question</param>
        /// <returns>Un booléen qui répond à la question</returns>
        public static bool EstActif(Utilisateur? utilisateur) => utilisateur is not null && utilisateur.DateFinu is null;

        /// <summary>
        /// Une fonction qui vérifie si l'utilisateur porte le claim Refuse (Si oui, il est interdit d'accès)
        /// </summary>
        /// <param name="utilisateur">Utilisateur en question</param>
        /// <returns>Un booléen qui répond à la question</returns>
        public static bool EstRefuse(ClaimsPrincipal utilisateur) => utilisateur.HasClaim(Refuse, "1");

        /// <summary>
        /// Durée pendant laquelle l'état d'une session est gardé en mémoire : la vérification a lieu à chaque requête (pages, images, scripts),
        /// on évite ainsi d'interroger la base à chaque fois. Une suppression ou un changement de droit en base est donc pris en compte
        /// au plus tard après ce délai, sans que l'utilisateur ait à se reconnecter.
        /// </summary>
        public static readonly TimeSpan DureeVerificationSession = TimeSpan.FromSeconds(10);

        /// <summary>
        /// État actuel d'une session ouverte : l'utilisateur est-il toujours actif, et quel est son droit aujourd'hui en base ?
        /// </summary>
        /// <param name="Valide">Vrai si la session peut continuer</param>
        /// <param name="Droit">Droit actuel (R, W, A ou S) ; vide si l'état n'a pas à être mis à jour (compte refusé, session invalide)</param>
        public sealed record EtatSession(bool Valide, string? Droit);

        /// <summary>
        /// Fonction qui lit l'état d'une session ouverte (cookie) dans la base. Si l'utilisateur a été supprimé ou n'existe plus, la session
        /// n'est plus valable : l'application la rejette et supprime le cookie. Sinon, le droit actuel est renvoyé pour mettre le cookie à jour
        /// (voir Program.cs). Un compte déjà refusé n'a rien à vérifier : il n'a accès à aucune page protégée.
        /// </summary>
        /// <param name="utilisateur">Utilisateur de la session</param>
        /// <param name="db">Base de données</param>
        /// <param name="cache">Mémoire où garder le résultat pendant <see cref="DureeVerificationSession"/></param>
        public static async Task<EtatSession> EtatSessionAsync(ClaimsPrincipal utilisateur, AppDbContext db, IMemoryCache cache)
        {
            if (EstRefuse(utilisateur)) return new EtatSession(true, null);

            var email = utilisateur.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return new EtatSession(false, null);

            var cle = "session:" + email.ToLowerInvariant();
            if (cache.TryGetValue(cle, out EtatSession? etat) && etat is not null) return etat;

            var user = await db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.UserEmail == email);
            etat = EstActif(user) ? new EtatSession(true, user!.Access) : new EtatSession(false, null);
            cache.Set(cle, etat, DureeVerificationSession);
            return etat;
        }

        /// <summary>
        /// Fonction qui indique si une session ouverte peut continuer (voir <see cref="EtatSessionAsync"/>)
        /// </summary>
        public static async Task<bool> SessionEstValideAsync(ClaimsPrincipal utilisateur, AppDbContext db, IMemoryCache cache) =>
            (await EtatSessionAsync(utilisateur, db, cache)).Valide;

        /// <summary>
        /// être connecté ET ne pas être un compte refusé.
        /// Un échec renvoie vers /Account/AccessDenied, donc vers la page « accès refusé ».
        /// </summary>
        /// <returns>Une autorisation qui définit le contrôle d'accès aux pages</returns>
        public static AuthorizationPolicy PolitiqueParDefaut() => new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => !EstRefuse(ctx.User))
            .Build();
    }
}
