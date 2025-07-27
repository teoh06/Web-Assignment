using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Demo.Models;

namespace Demo.Controllers;

public class CategoryController : Controller
{
    private readonly DB db;
    public CategoryController(DB db)
    {
        this.db = db;
    }

    // GET: /Category
    public IActionResult Index()
    {
        return View(db.Categories.ToList());
    }

    // GET: /Category/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Category category)
    {
        if (ModelState.IsValid)
        {
            db.Categories.Add(category);
            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // GET: /Category/Edit/5
    public IActionResult Edit(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: /Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Category category)
    {
        if (id != category.CategoryId) return NotFound();
        if (ModelState.IsValid)
        {
            db.Entry(category).State = EntityState.Modified;
            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // GET: /Category/Delete/5
    public IActionResult Delete(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: /Category/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var category = db.Categories.Find(id);
        if (category != null)
        {
            db.Categories.Remove(category);
            db.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: /Category/Details/5
    public IActionResult Details(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }
}
