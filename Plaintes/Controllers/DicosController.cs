
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plaintes.Models;
using Plaintes.Data;

public class DicosController : Controller
{
    private readonly AppDbContext _context;

    public DicosController(AppDbContext context)
    {
        _context = context;
    }

    // GET: DICOS
    [Route("/dico")]
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Dicos.ToListAsync());
    }

    // GET: DICOS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dico = await _context.Dicos
            .FirstOrDefaultAsync(m => m.Id == id);
        if (dico == null)
        {
            return NotFound();
        }

        return View(dico);
    }

    // GET: DICOS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: DICOS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Nomtable,Champs,Description")] Dico dico)
    {
        if (ModelState.IsValid)
        {
            _context.Add(dico);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(dico);
    }

    // GET: DICOS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dico = await _context.Dicos.FindAsync(id);
        if (dico == null)
        {
            return NotFound();
        }
        return View(dico);
    }

    // POST: DICOS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nomtable,Champs,Description")] Dico dico)
    {
        if (id != dico.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(dico);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DicoExists(dico.Id))
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
        return View(dico);
    }

    // GET: DICOS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dico = await _context.Dicos
            .FirstOrDefaultAsync(m => m.Id == id);
        if (dico == null)
        {
            return NotFound();
        }

        return View(dico);
    }

    // POST: DICOS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var dico = await _context.Dicos.FindAsync(id);
        if (dico != null)
        {
            _context.Dicos.Remove(dico);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DicoExists(int? id)
    {
        return _context.Dicos.Any(e => e.Id == id);
    }
}
