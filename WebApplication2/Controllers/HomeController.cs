using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

public class HomeController : Controller
{
    private readonly DB _dbContext;
    private readonly IWebHostEnvironment _hostingEnvironment;

    public HomeController(DB dbContext, IWebHostEnvironment hostingEnvironment)
    {
        _dbContext = dbContext;
        _hostingEnvironment = hostingEnvironment;
    }

    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }


    // GET: Home/Both
    [Authorize]
    public IActionResult Both()
    {
        return View();
    }
    [Authorize(Roles = "Guest")]
    public IActionResult Guest()
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

    // GET: Home/Report
    public IActionResult Report()
    {
        return View();
    }

    // GET: Home/Privacy
    public IActionResult Privacy()
    {
        return View();
    }

    


}
