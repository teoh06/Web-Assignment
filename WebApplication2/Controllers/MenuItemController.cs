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

        menuItemsQuery = menuItemsQuery.OrderBy(m => m.Name);

        // Determine if the current user is in the "Member" role
        bool isMember = User.IsInRole("Member");

        // Select only necessary properties to send back as JSON
        var items = await menuItemsQuery.Select(m => new
        {
            m.MenuItemId,
            m.Name,
            m.Description,
            Price = m.Price.ToString("C", new System.Globalization.CultureInfo("en-MY")), // Format price for display
            CategoryName = m.Category.Name, // Get category name
            PhotoURL = m.PhotoURL // Assuming PhotoURL is a string path
        }).ToListAsync();

        return Json(items); // Return filtered items as JSON
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

    // GET: /MenuItem/Edit/
    public IActionResult Edit(int id)
    {
        var menuItem = db.MenuItems.Find(id);
        if (menuItem == null) return NotFound();
        ViewBag.Categories = db.Categories.ToList();
        return View(menuItem);
    }

    // POST: /MenuItem/Edit/
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

    // GET: /MenuItem/Delete/
    public IActionResult Delete(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category).FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        return View(menuItem);
    }

    // POST: /MenuItem/Delete/
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

    // GET: /MenuItem/Details/
    public IActionResult Details(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category).FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        var ratings = db.MenuItemRatings.Where(r => r.MenuItemId == id).ToList();
        var comments = db.MenuItemComments.Include(c => c.Member).Where(c => c.MenuItemId == id).OrderByDescending(c => c.CommentedAt).ToList();
        var vm = new MenuItemDetailsVM
        {
            MenuItem = menuItem,
            Ratings = ratings,
            Comments = comments
        };
        return View(vm);
    }

    [HttpPost]
    public IActionResult AddRating(int menuItemId, int value)
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var existing = db.MenuItemRatings.FirstOrDefault(r => r.MenuItemId == menuItemId && r.MemberEmail == email);
        if (existing != null)
        {
            existing.Value = value;
            existing.RatedAt = DateTime.Now;
        }
        else
        {
            db.MenuItemRatings.Add(new MenuItemRating { MenuItemId = menuItemId, Value = value, MemberEmail = email });
        }
        db.SaveChanges();
        var ratings = db.MenuItemRatings.Where(r => r.MenuItemId == menuItemId).ToList();
        return Json(new { avg = ratings.Count > 0 ? ratings.Average(r => r.Value) : 0, count = ratings.Count });
    }

    [HttpPost]
    public IActionResult AddComment(int menuItemId, string content)
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var comment = new MenuItemComment { MenuItemId = menuItemId, Content = content, MemberEmail = email };
        db.MenuItemComments.Add(comment);
        db.SaveChanges();
        return Json(new { user = email, content, time = comment.CommentedAt });
    }
}
