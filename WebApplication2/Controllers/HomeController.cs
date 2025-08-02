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
        // Get 3 featured menu items (e.g., by price descending, or just take 3)
        var featured = db.MenuItems
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

    // _Layout.cshtml - Set language
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );

        return LocalRedirect(returnUrl ?? "/");
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
