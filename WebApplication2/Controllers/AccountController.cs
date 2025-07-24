using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication2.Models;
using System.Net.Mail;
using MailAttachment = System.Net.Mail.Attachment;

namespace WebApplication2.Controllers;

public class AccountController : Controller
{

    private readonly DB _context;
    private readonly Helper _helper;
    private readonly IWebHostEnvironment _environment;

    public AccountController(DB context, Helper helper, IWebHostEnvironment environment)
        {
            this._context = context;
            this._helper = helper;
            this._environment =  environment;
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

    public IActionResult ResetPassword()
    {
        return View();
    }

    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var u = _context.Users.FirstOrDefault(u => u.Email == model.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
            return View(model);
        }

        string password = _helper.RandomPassword();
        u.PasswordHash = _helper.HashPassword(password);
        _context.SaveChanges();

        sendResetPasswordEmail(u, password);

        TempData["Info"] = "Password reset successful. Check your email.";
        return RedirectToAction("Login");
    }


    private void sendResetPasswordEmail(User u, string password)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Reset Password";
        mail.IsBodyHtml = true;

        var url = Url.Action("Login", "Account", null, "https");

        string? path = null;

        if (u is Admin)
        {
            path = Path.Combine(_environment.WebRootPath, "photos", "edb1c48494e9459e98d187f8edf7a044.jpg");
        }
        else if (u is Member m && !string.IsNullOrWhiteSpace(m.PhotoURL))
        {
            path = Path.Combine(_environment.WebRootPath, "photos", "edb1c48494e9459e98d187f8edf7a044.jpg");
        }
        else
        {
            path = Path.Combine(_environment.WebRootPath, "photos", "edb1c48494e9459e98d187f8edf7a044.jpg");
        }

        // Check that the path is valid and file exists before attaching
        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
        {
            var att = new MailAttachment(path);
            att.ContentId = "photo";
            mail.Attachments.Add(att);

            mail.Body = $@"
            <img src='cid:photo' style='width: 200px; height: 200px;
                                        border: 1px solid #333'>
            <p>Dear {u.Name},</p>
            <p>Your password has been reset to:</p>
            <h1 style='color: red'>{password}</h1>
            <p>
                Please <a href='{url}'>login</a> with your new password.
            </p>
            <p>From, 🐱 Super Admin</p>
        ";
        }
        else
        {
            // fallback email without image
            mail.Body = $@"
            <p>Dear {u.Name},</p>
            <p>Your password has been reset to:</p>
            <h1 style='color: red'>{password}</h1>
            <p>
                Please <a href='{url}'>login</a> with your new password.
            </p>
            <p>From, 🐱 Super Admin</p>
        ";
        }

        _helper.SendEmail(mail);
    }

}
