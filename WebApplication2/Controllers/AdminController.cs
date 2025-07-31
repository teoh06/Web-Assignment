using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;


namespace WebApplication2.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly DB _context;

    public AdminController(DB _context)
    {
        this._context = _context ;
    }

    public IActionResult Index()
    {

        if(User.IsInRole("Admin"))
        {
            return View();
        }


        return Forbid();
    }

    public IActionResult ManageUsers()
    {
        var admin = _context.Admins.ToList<User>();
        var member = _context.Members.ToList<User>();

        var all = admin.Concat(member).ToList();
        return View(all);
    }

}
