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
    public IActionResult Index(string search, decimal? minPrice, decimal? maxPrice, int? categoryId)
    {
        var items = db.MenuItems.Include(m => m.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(m => m.Name.Contains(search));

        if (minPrice.HasValue)
            items = items.Where(m => m.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            items = items.Where(m => m.Price <= maxPrice.Value);

        if (categoryId.HasValue && categoryId.Value > 0)
            items = items.Where(m => m.CategoryId == categoryId.Value);

        // Pass current filter values to the view
        ViewBag.CurrentSearch = search ?? "";
        ViewBag.CurrentCategoryId = categoryId ?? 0;
        ViewBag.CurrentMinPrice = minPrice;
        ViewBag.CurrentMaxPrice = maxPrice;

        var vm = new MenuItemIndexVM
        {
            MenuItems = items.OrderBy(m => m.Name).ToList(),
            Categories = db.Categories.ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetFilteredMenuItems(string search, int? categoryId, decimal? minPrice, decimal? maxPrice)
    {
        var menuItemsQuery = db.MenuItems.Include(m => m.Category).AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.Name.Contains(search));
        }

        // Apply category filter
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.CategoryId == categoryId.Value);
        }

        // Apply price filters
        if (minPrice.HasValue)
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            menuItemsQuery = menuItemsQuery.Where(m => m.Price <= maxPrice.Value);
        }

        // Order results
        menuItemsQuery = menuItemsQuery.OrderBy(m => m.Name);

        // Select only necessary properties for JSON response
        var items = await menuItemsQuery.Select(m => new
        {
            m.MenuItemId,
            m.Name,
            m.Description,
            Price = m.Price.ToString("C", new System.Globalization.CultureInfo("en-MY")),
            CategoryName = m.Category.Name,
            PhotoURL = m.PhotoURL
        }).ToListAsync();

        return Json(items);
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
    public IActionResult Create(MenuItem menuItem, List<IFormFile> ImageFiles)
    {
        if (ModelState.IsValid)
        {
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                var helper = new Helper(HttpContext.RequestServices.GetService<IWebHostEnvironment>(),
                                       HttpContext.RequestServices.GetService<IHttpContextAccessor>(),
                                       HttpContext.RequestServices.GetService<IConfiguration>());
                foreach (var imageFile in ImageFiles)
                {
                    var err = helper.ValidatePhoto(imageFile);
                    if (!string.IsNullOrEmpty(err))
                    {
                        ModelState.AddModelError("PhotoURL", err);
                        ViewBag.Categories = db.Categories.ToList();
                        return View(menuItem);
                    }
                    var fileName = helper.SavePhoto(imageFile, "images");
                    db.MenuItemImages.Add(new MenuItemImage {
                        MenuItem = menuItem,
                        FileName = fileName,
                        UploadDate = DateTime.Now
                    });
                    // Set first image as PhotoURL for backward compatibility
                    if (string.IsNullOrEmpty(menuItem.PhotoURL))
                        menuItem.PhotoURL = fileName;
                }
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
    public IActionResult Edit(int id, MenuItem menuItem, List<IFormFile> ImageFiles)
    {
        if (id != menuItem.MenuItemId) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = db.MenuItems.AsNoTracking().FirstOrDefault(m => m.MenuItemId == id);
            if (existing == null) return NotFound();
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                var helper = new Helper(HttpContext.RequestServices.GetService<IWebHostEnvironment>(),
                                       HttpContext.RequestServices.GetService<IHttpContextAccessor>(),
                                       HttpContext.RequestServices.GetService<IConfiguration>());
                foreach (var imageFile in ImageFiles)
                {
                    var err = helper.ValidatePhoto(imageFile);
                    if (!string.IsNullOrEmpty(err))
                    {
                        ModelState.AddModelError("PhotoURL", err);
                        ViewBag.Categories = db.Categories.ToList();
                        return View(menuItem);
                    }
                    var fileName = helper.SavePhoto(imageFile, "images");
                    db.MenuItemImages.Add(new MenuItemImage {
                        MenuItemId = menuItem.MenuItemId,
                        FileName = fileName,
                        UploadDate = DateTime.Now
                    });
                    // Set first image as PhotoURL for backward compatibility
                    if (string.IsNullOrEmpty(menuItem.PhotoURL))
                        menuItem.PhotoURL = fileName;
                }
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
        var menuItem = db.MenuItems.Include(m => m.Category)
                                   .Include(m => m.MenuItemImages)
                                   .FirstOrDefault(m => m.MenuItemId == id);
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


