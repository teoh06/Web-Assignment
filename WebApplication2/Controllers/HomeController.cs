using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace WebApplication2.Controllers;

public class HomeController : Controller
{
    private readonly DB db;
    public HomeController(DB db)
    {
        this.db = db;
    }

    // GET: Home/Index
    public IActionResult Index()
    {
        var query = db.MenuItems.AsQueryable();

        // Hide inactive items for non-admins
        if (!User.IsInRole("Admin"))
        {
            query = query.Where(m => m.IsActive);
        }

        var featured = query
            .OrderByDescending(m => m.Price)
            .Take(4)
            .Select(m => new FeaturedMenuItemVM
            {
                MenuItemId = m.MenuItemId,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                Image = "/images/" + (string.IsNullOrEmpty(m.PhotoURL) ? "default.jpg" : m.PhotoURL)
            })
            .ToList();

        return View(featured);
    }



    // GET: Home/Both
    [Authorize]
    public IActionResult Both()
    {
        return View();
    }

    // GET: Home/Member
    [Authorize(Roles = "Member")]
    public IActionResult Member()
    {
        return View();
    }

    // GET: Home/Admin
    [Authorize(Roles = "Admin")]
    public IActionResult Admin()
    {
        return View();
    }

    // GET: Home/Privacy
    public IActionResult Privacy()
    {
        return View();
    }
}
public class FeaturedMenuItemVM
{
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Image { get; set; }
}
