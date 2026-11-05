using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plaintes.Models;
using Plaintes.Services.Export;
using Plaintes.Services.IServices;

namespace Plaintes.Controllers
{
    [Route("Statistiques")]
    public class StatistiquesController : Controller
    {
        private readonly IStatistiquesSrv _statistiquesSrv;
        private readonly ILogger<StatistiquesController> _logger;

        public StatistiquesController(IStatistiquesSrv statistiquesSrv, ILogger<StatistiquesController> logger)
        {
            _statistiquesSrv = statistiquesSrv;
            _logger = logger;
        }

        /// <summary>
        /// Action qui affiche la page des statistiques avec les filtres lus dans l'adresse
        /// </summary>
        /// <param name="filtres">Filtres choisis, tous facultatifs</param>
        [HttpGet("")]
        [Authorize]
        public async Task<IActionResult> Index([FromQuery] StatistiquesFiltres filtres)
        {
            return View(new StatistiquesPageViewModel
            {
                Filtres = filtres,
                Resultat = await _statistiquesSrv.GetStatistiques(filtres),
                Options = await _statistiquesSrv.GetOptions()
            });
        }

        /// <summary>
        /// Action appelée par la page quand on change un filtre
        /// </summary>
        /// <param name="filtres">Filtres choisis, tous facultatifs</param>
        [HttpGet("Donnees")]
        [Authorize]
        public async Task<IActionResult> Donnees([FromQuery] StatistiquesFiltres filtres)
        {
            return Json(await _statistiquesSrv.GetStatistiques(filtres));
        }

        /// <summary>
        /// Action qui télécharge en Excel les statistiques calculées avec les filtres de la page (mêmes paramètres que l'adresse de la page)
        /// </summary>
        /// <param name="filtres">Filtres choisis, tous facultatifs</param>
        /// <param name="excel">Service qui fabrique le classeur</param>
        [HttpGet("Excel")]
        [Authorize]
        public async Task<IActionResult> ExportExcel([FromQuery] StatistiquesFiltres filtres, [FromServices] IExportExcelSrv excel)
        {
            var resultat = await _statistiquesSrv.GetStatistiques(filtres);
            var rappel = await _statistiquesSrv.DecrireFiltres(filtres);
            _logger.LogInformation("Export Excel des statistiques ({Nombre} dossier(s)) par {Utilisateur} (filtres : {Filtres})", resultat.Total,
                User.Identity?.Name, rappel.Count == 0 ? "aucun" : string.Join(" ; ", rappel.Select(f => f.Libelle + " = " + f.Valeur)));
            return File(excel.Statistiques(resultat, rappel), ExportExcelSrv.TypeMime, $"Statistiques_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }
    }
}
