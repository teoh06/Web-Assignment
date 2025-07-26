using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

public class MenuItemController : Controller
{
    private readonly DB db;
    public MenuItemController(DB db)
    {
        this.db = db;
    }

    // GET: /MenuItem
    public IActionResult Index()
    {
        var items = db.MenuItems.Include(m => m.Category).ToList();
        return View(items);
    }

    // GET: /MenuItem/Create
    public IActionResult Create()
    {
        ViewBag.Categories = db.Categories.ToList();
        return View();
    }

    // POST: /MenuItem/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(MenuItem menuItem)
    {
        if (ModelState.IsValid)
        {
            db.MenuItems.Add(menuItem);
            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Categories = db.Categories.ToList();
        return View(menuItem);
    }

    // GET: /MenuItem/Edit/5
    public IActionResult Edit(int id)
    {
        var menuItem = db.MenuItems.Find(id);
        if (menuItem == null) return NotFound();
        ViewBag.Categories = db.Categories.ToList();
        return View(menuItem);
    }

    // POST: /MenuItem/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, MenuItem menuItem)
    {
        if (id != menuItem.MenuItemId) return NotFound();
        if (ModelState.IsValid)
        {
            db.Entry(menuItem).State = EntityState.Modified;
            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Categories = db.Categories.ToList();
        return View(menuItem);
    }

    // GET: /MenuItem/Delete/5
    public IActionResult Delete(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category).FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        return View(menuItem);
    }

    // POST: /MenuItem/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var menuItem = db.MenuItems.Find(id);
        if (menuItem != null)
        {
            db.MenuItems.Remove(menuItem);
            db.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: /MenuItem/Details/5
    public IActionResult Details(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category).FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        return View(menuItem);
    }
}
