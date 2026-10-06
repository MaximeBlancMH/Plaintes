using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;
using System.Diagnostics;

namespace Plaintes.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IPlainteSrv _plainteSrv;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IPlainteSrv plainteSrv)
        {
            _logger = logger;
            _context = context;
            _plainteSrv = plainteSrv;
        }

        [Route("")]
        public IActionResult Index() => RedirectToAction("Dossiers", "Dossier");

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }


        public IActionResult GestionTables()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
