using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Plaintes.Services.Stockage
{
    /// <summary>
    /// Relève la limite de taille de la requête pour l'action qui reçoit les pièces jointes (Storage:MaxTotalSize + une marge
    /// pour les champs du formulaire). Le reste de l'application garde la limite par défaut de Kestrel (30 Mo).
    /// C'est un filtre d'autorisation placé en tête (Order négatif) : [ValidateAntiForgeryToken] lit déjà le formulaire à ce stade,
    /// donc la limite doit être relevée avant lui.
    /// </summary>
    public class LimiteEnvoiPiecesFilter : IAuthorizationFilter
    {
        private const long MargeFormulaire = 1024 * 1024;
        private readonly StorageSettings _s;

        public LimiteEnvoiPiecesFilter(IOptions<StorageSettings> s) => _s = s.Value;

        /// <summary>
        /// Écrit la taille maximale du corps de la requête
        /// </summary>
        /// <param name="context"></param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var limite = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false })
                limite.MaxRequestBodySize = _s.MaxTotalSize + MargeFormulaire;
        }

    }
}
