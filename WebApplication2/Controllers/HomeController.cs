using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Demo.Controllers;

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
        // Get 3 featured menu items (e.g., by price descending, or just take 3)
        var featured = db.MenuItems
            .OrderByDescending(m => m.Price)
            .Take(3)
            .Select(m => new FeaturedMenuItemVM
            {
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                Image = "/images/" + (string.IsNullOrEmpty(m.PhotoURL) ? "default.png" : m.PhotoURL)
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
}
public class FeaturedMenuItemVM
{
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Image { get; set; }
}
