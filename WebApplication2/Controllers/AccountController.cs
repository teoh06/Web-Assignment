using Microsoft.AspNetCore.Mvc;

namespace WebApplication2.Controllers;

public class AccountController : Controller
{

    private readonly DB _context;
    private readonly Helper _helper;

    public AccountController(DB context, Helper helper)
        {
            _context = context;
            _helper = helper;
        }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    
    [HttpPost]
    public IActionResult Register(RegisterVM model)
    {
        if(!ModelState.IsValid)
        {
            return View(model);
        }

        if (_context.Users.Any(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email already exists.");
            return View(model);
        }

        string photoFile = null;

        if(model.ProfilePicture != null)
        {
            var error = _helper.ValidatePhoto(model.ProfilePicture);
            if(!string.IsNullOrEmpty(error))
            {
                ModelState.AddModelError("ProfilePicture", error);
                return View(model);
            }

            photoFile = _helper.SavePhoto(model.ProfilePicture, "photos");
        }

        var user = new User
        {
            Email = model.Email,
            PasswordHash = _helper.HashPassword(model.Password),
            Name = model.Name,
            RoleType = model.RoleType,
            PhotoPath = photoFile
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        _helper.SignIn(user.Email , user.RoleType , rememberMe: true);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Login(LoginVM model)
    {
        if(!ModelState.IsValid)
        {
            return View(model);
        }
        var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

        if(user == null || !_helper.VerifyPassword(user.PasswordHash, model.Password) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }
        _helper.SignIn(user.Email, user.RoleType, model.RememberMe);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Logout()
    {
        _helper.SignOut();
        return RedirectToAction("Login");
    }

}
