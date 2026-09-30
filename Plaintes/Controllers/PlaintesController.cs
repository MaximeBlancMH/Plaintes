using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;

namespace Plaintes.Controllers
{
    [Route("/plaintes")]
    public class PlaintesController : Controller
    {
        private readonly AppDbContext _context;

        public PlaintesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Plaintes
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Plaintes.Where(p => p.Datedeb != null && p.Datefin != null).Include(p => p.CodecatNavigation).Include(p => p.CodepartiecommuneNavigation).Include(p => p.CoderueNavigation).Include(p => p.CodetatNavigation).Include(p => p.CodeuserNavigation).Include(p => p.NumciteNavigation);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Plaintes/Details/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var plainte = await _context.Plaintes
                .Include(p => p.CodecatNavigation)
                .Include(p => p.CodepartiecommuneNavigation)
                .Include(p => p.CoderueNavigation)
                .Include(p => p.CodetatNavigation)
                .Include(p => p.CodeuserNavigation)
                .Include(p => p.NumciteNavigation)
                .FirstOrDefaultAsync(m => m.Numplainte == id);
            if (plainte == null)
            {
                return NotFound();
            }

            return View(plainte);
        }

        // GET: Plaintes/Create
        public IActionResult Create()
        {
            ViewData["Codecat"] = new SelectList(_context.Categories, "Codecat", "Codecat");
            ViewData["Codepartiecommune"] = new SelectList(_context.PartieCommunes, "Codepartiecommune", "Codepartiecommune");
            ViewData["Coderue"] = new SelectList(_context.Rues, "Coderue", "Coderue");
            ViewData["Codetat"] = new SelectList(_context.Etats, "Codetat", "Codetat");
            ViewData["Codeuser"] = new SelectList(_context.Utilisateurs, "Codeuser", "Codeuser");
            ViewData["Numcite"] = new SelectList(_context.Cites, "Numcite", "Numcite");
            return View();
        }

        // POST: Plaintes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Numplainte,Codeuser,Codepartiecommune,Codecat,Numcite,Datedeb,Datefin,Numreclamation,Commentaire,Suppr,Numentree,Coderue,Codetat,Numrue")] Plainte plainte)
        {
            if (ModelState.IsValid)
            {
                _context.Add(plainte);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["Codecat"] = new SelectList(_context.Categories, "Codecat", "Codecat", plainte.Codecat);
            ViewData["Codepartiecommune"] = new SelectList(_context.PartieCommunes, "Codepartiecommune", "Codepartiecommune", plainte.Codepartiecommune);
            ViewData["Coderue"] = new SelectList(_context.Rues, "Coderue", "Coderue", plainte.Coderue);
            ViewData["Codetat"] = new SelectList(_context.Etats, "Codetat", "Codetat", plainte.Codetat);
            ViewData["Codeuser"] = new SelectList(_context.Utilisateurs, "Codeuser", "Codeuser", plainte.Codeuser);
            ViewData["Numcite"] = new SelectList(_context.Cites, "Numcite", "Numcite", plainte.Numcite);
            return View(plainte);
        }

        // GET: Plaintes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var plainte = await _context.Plaintes.FindAsync(id);
            if (plainte == null)
            {
                return NotFound();
            }
            ViewData["Codecat"] = new SelectList(_context.Categories, "Codecat", "Codecat", plainte.Codecat);
            ViewData["Codepartiecommune"] = new SelectList(_context.PartieCommunes, "Codepartiecommune", "Codepartiecommune", plainte.Codepartiecommune);
            ViewData["Coderue"] = new SelectList(_context.Rues, "Coderue", "Coderue", plainte.Coderue);
            ViewData["Codetat"] = new SelectList(_context.Etats, "Codetat", "Codetat", plainte.Codetat);
            ViewData["Codeuser"] = new SelectList(_context.Utilisateurs, "Codeuser", "Codeuser", plainte.Codeuser);
            ViewData["Numcite"] = new SelectList(_context.Cites, "Numcite", "Numcite", plainte.Numcite);
            return View(plainte);
        }

        // POST: Plaintes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Numplainte,Codeuser,Codepartiecommune,Codecat,Numcite,Datedeb,Datefin,Numreclamation,Commentaire,Suppr,Numentree,Coderue,Codetat,Numrue")] Plainte plainte)
        {
            if (id != plainte.Numplainte)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(plainte);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PlainteExists(plainte.Numplainte))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Codecat"] = new SelectList(_context.Categories, "Codecat", "Codecat", plainte.Codecat);
            ViewData["Codepartiecommune"] = new SelectList(_context.PartieCommunes, "Codepartiecommune", "Codepartiecommune", plainte.Codepartiecommune);
            ViewData["Coderue"] = new SelectList(_context.Rues, "Coderue", "Coderue", plainte.Coderue);
            ViewData["Codetat"] = new SelectList(_context.Etats, "Codetat", "Codetat", plainte.Codetat);
            ViewData["Codeuser"] = new SelectList(_context.Utilisateurs, "Codeuser", "Codeuser", plainte.Codeuser);
            ViewData["Numcite"] = new SelectList(_context.Cites, "Numcite", "Numcite", plainte.Numcite);
            return View(plainte);
        }

        // GET: Plaintes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var plainte = await _context.Plaintes
                .Include(p => p.CodecatNavigation)
                .Include(p => p.CodepartiecommuneNavigation)
                .Include(p => p.CoderueNavigation)
                .Include(p => p.CodetatNavigation)
                .Include(p => p.CodeuserNavigation)
                .Include(p => p.NumciteNavigation)
                .FirstOrDefaultAsync(m => m.Numplainte == id);
            if (plainte == null)
            {
                return NotFound();
            }

            return View(plainte);
        }

        // POST: Plaintes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var plainte = await _context.Plaintes.FindAsync(id);
            if (plainte != null)
            {
                _context.Plaintes.Remove(plainte);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PlainteExists(int id)
        {
            return _context.Plaintes.Any(e => e.Numplainte == id);
        }
    }
}
