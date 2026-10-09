using Microsoft.AspNetCore.Mvc;
using Plaintes.Services.IServices;

namespace Plaintes.Controllers
{
    [Route("Statistiques")]
    public class StatistiquesController : Controller
    {
        private readonly IStatistiquesSrv _statistiquesSrv;

        public StatistiquesController(IStatistiquesSrv statistiquesSrv)
        {
            _statistiquesSrv = statistiquesSrv;
        }

        /// <summary>
        /// Action qui affiche les statistiques des dossiers, pour une année ou pour toutes les années
        /// </summary>
        /// <param name="annee">Année choisie, vide pour toutes les années</param>
        [HttpGet("")]
        public async Task<IActionResult> Index(int? annee)
        {
            return View(await _statistiquesSrv.GetStatistiques(annee));
        }
    }
}
