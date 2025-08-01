using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic; // For List<T>
using System.Linq; // For LINQ queries like Where, OrderBy, Select
using System.Threading.Tasks; // For async/await
using WebApplication2.Models; // Ensure this namespace matches your DB context and models
using WebApplication2; // For Helper

namespace WebApplication2.Controllers;

public class MenuItemController : Controller
{
    private readonly DB db; 

    public MenuItemController(DB db)
    {
        this.db = db;
    }

    // GET: /MenuItem/Index
    // This action will handle search queries and category filters for the initial page load
    public async Task<IActionResult> Index(string searchString, int? categoryId)
    {
        // Prepare the base query for menu items, including their categories
        var menuItemsQuery = from m in db.MenuItems
                             select m;

        menuItemsQuery = menuItemsQuery.Include(m => m.Category);

        // Apply search filter if searchString is provided
        if (!string.IsNullOrEmpty(searchString))
        {
            // Case-insensitive search on MenuItem Name
            menuItemsQuery = menuItemsQuery.Where(s => s.Name.Contains(searchString));
        }

        // Apply category filter if categoryId is provided (and not "All Categories" which is 0)
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.CategoryId == categoryId.Value);
        }

        // Order by Name for consistent display
        menuItemsQuery = menuItemsQuery.OrderBy(m => m.Name);

        // Fetch all categories to populate the filter dropdown in the view
        var categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();

        // Create a ViewModel to hold both menu items and categories, plus current filter values
        var viewModel = new MenuItemIndexVM
        {
            MenuItems = await menuItemsQuery.ToListAsync(), // Execute the filtered query
            Categories = categories,
            SearchString = searchString, // Pass back current search string to keep input field populated
            SelectedCategoryId = categoryId // Pass back current category ID to keep dropdown selected
        };

        return View(viewModel);
    }

    // NEW ACTION: This action will be called by AJAX requests for filtered menu items
    [HttpGet] // Or [HttpPost] if you prefer, but GET is common for filtering data
    public async Task<IActionResult> GetFilteredMenuItems(string searchString, int? categoryId)
    {
        var menuItemsQuery = from m in db.MenuItems
                             select m;

        menuItemsQuery = menuItemsQuery.Include(m => m.Category);

        if (!string.IsNullOrEmpty(searchString))
        {
            menuItemsQuery = menuItemsQuery.Where(s => s.Name.Contains(searchString));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.CategoryId == categoryId.Value);
        }

        menuItemsQuery = menuItemsQuery.OrderBy(m => m.Name);

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
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewBag.Categories = db.Categories.ToList();
        return View();
    }

    // POST: /MenuItem/Create
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(MenuItem menuItem, IFormFile ImageFile)
    {
        if (ModelState.IsValid)
        {
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var helper = new Helper(HttpContext.RequestServices.GetService<IWebHostEnvironment>(),
                                       HttpContext.RequestServices.GetService<IHttpContextAccessor>(),
                                       HttpContext.RequestServices.GetService<IConfiguration>());
                var err = helper.ValidatePhoto(ImageFile);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("PhotoURL", err);
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem);
                }
                menuItem.PhotoURL = helper.SavePhoto(ImageFile, "images");
            }
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
    public IActionResult Edit(int id, MenuItem menuItem, IFormFile ImageFile)
    {
        if (id != menuItem.MenuItemId) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = db.MenuItems.AsNoTracking().FirstOrDefault(m => m.MenuItemId == id);
            if (existing == null) return NotFound();
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var helper = new Helper(HttpContext.RequestServices.GetService<IWebHostEnvironment>(),
                                       HttpContext.RequestServices.GetService<IHttpContextAccessor>(),
                                       HttpContext.RequestServices.GetService<IConfiguration>());
                var err = helper.ValidatePhoto(ImageFile);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("PhotoURL", err);
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem);
                }
                // Optionally delete old photo
                if (!string.IsNullOrEmpty(existing.PhotoURL))
                    helper.DeletePhoto(existing.PhotoURL, "images");
                menuItem.PhotoURL = helper.SavePhoto(ImageFile, "images");
            }
            else if (string.IsNullOrWhiteSpace(menuItem.PhotoURL))
            {
                menuItem.PhotoURL = existing.PhotoURL;
            }
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


