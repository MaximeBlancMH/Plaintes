using Microsoft.AspNetCore.Authorization;
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
