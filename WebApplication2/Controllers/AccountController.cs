using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using WebApplication2.Services;
using WebApplication2.Models;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System; 
using System.Linq;
using MailAttachment = System.Net.Mail.Attachment;
using Microsoft.AspNetCore.Identity.UI.Services;
namespace WebApplication2.Controllers;

public class AccountController : Controller
{

    private readonly DB _context;
    private readonly Helper _helper;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;

    public AccountController(DB context, Helper helper, IWebHostEnvironment environment, IEmailService emailService)
        {
            this._context = context;
            this._helper = helper;
            this._environment =  environment;
            this._emailService = emailService;
    }

    [HttpGet]
    public IActionResult Login()
    { 
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginVM model, string? returnURL)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

        if (user == null || !_helper.VerifyPassword(user.Hash, model.Password) || string.IsNullOrWhiteSpace(user.Hash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        else if (user.IsPendingDeletion) // Add this condition
        {
            ModelState.AddModelError("", "This account is pending deletion. Please check your email for restoration options.");
            return View(model);
        }

        
        if(ModelState.IsValid)
        {
            TempData["Info"] = "Login successfully.";
            await _helper.SignIn(user, model.RememberMe);

            if (!string.IsNullOrEmpty(returnURL))
            {
                return Redirect(returnURL);
            }
            return RedirectToAction("Index", "Home");
        }
        
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> Register(RegisterVM model)
    {
        Console.WriteLine("REGISTER POST START"); // Log start
        Console.WriteLine($"Model Valid: {ModelState.IsValid}");
        Console.WriteLine($"Email: {model.Email}");
        Console.WriteLine($"ProfilePicture: {model.ProfilePicture?.FileName}");
        if (!ModelState.IsValid)
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


        var user = new Member
        {
            Email = model.Email,
            Hash = _helper.HashPassword(model.Password),
            Name = model.Name,
            PhotoURL = photoFile
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        await _helper.SignIn(user, rememberMe: true);

        return RedirectToAction("Index", "Home");
    }

    

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _helper.SignOut(); // Sign out user (clears cookie, session, etc.)
        TempData["Info"] = "Logout successful.";
        return RedirectToAction("Login", "Account");
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
        u.Hash = _helper.HashPassword(password);
        _context.SaveChanges();

        sendResetPasswordEmail(u, password);

        TempData["Info"] = "Password reset successful. Check your email.";
        return RedirectToAction("Login");
    }


    private void sendResetPasswordEmail(User u, string password)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        if (u == null || string.IsNullOrEmpty(u.Email) || string.IsNullOrEmpty(u.Name))
        {
            throw new Exception("User data incomplete in sendResetPasswordEmail");
        }

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
            path = Path.Combine(_environment.WebRootPath, "photos", m.PhotoURL);
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

    [AcceptVerbs("Get", "Post")]
    public IActionResult CheckEmail(string email)
    {
        bool isAvailable = !_context.Users.Any(u => u.Email == email);
        return Json(isAvailable);
    }

    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }

    public IActionResult UpdatePassword()
    {
        return View();
    }


    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {

        var user = _context.Users.Find(User.Identity!.Name);
        if (user == null) return RedirectToAction("Index", "Home");

        // If current password not matched
        // TODO
        if (!_helper.VerifyPassword(user.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            // Update user password (hash)
            user.Hash = _helper.HashPassword(vm.New);
            _context.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction("UpdatePassword");
        }

        return View();
    }


    public IActionResult UpdateProfile()
    {
        // Get member record based on email (PK)
        // TODO
        var m = _context.Members.Find(User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    public IActionResult Index()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        Console.WriteLine($"[DEBUG] Logged-in user: {email}, Role: {role}");

        return View();
    }



    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        // Get member record based on email (PK)
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var m = _context.Members.Find(email);


        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.ProfilePicture != null)
        {
            var err = _helper.ValidatePhoto(vm.ProfilePicture);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            if (vm.ProfilePicture != null)
            {
                _helper.DeletePhoto(m.PhotoURL, "photos");
                m.PhotoURL = _helper.SavePhoto(vm.ProfilePicture, "photos");
            }

            _context.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction("UpdateProfile");
        }

        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> InitiateAccountDeletion()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == User.Identity.Name);
        if (user == null)
        {
            return Unauthorized();
        }

        user.IsPendingDeletion = true;
        user.DeletionRequestDate = DateTime.UtcNow;
        user.DeletionToken = Guid.NewGuid().ToString("N");
        await _context.SaveChangesAsync();

        // --- Send a confirmation email using the INJECTED EmailService ---
        string emailError = null;
        try
        {
            // Use the injected _emailService directly
            var recoveryLink = Url.Action("RestoreAccount", "Account", new { token = user.DeletionToken }, Request.Scheme);
            var permanentDeleteLink = Url.Action("PermanentDelete", "Account", new { token = user.DeletionToken }, Request.Scheme);

            string emailSubject = "Account Deletion Request";
            string emailBody = $@"
                <p>We have received a request to delete your account.</p>
                <p>If you did not make this request, please ignore this email.</p>
                <p>Your account will be permanently deleted in 7 days. If you change your mind, you can restore your account by clicking the link below:</p>
                <a href='{recoveryLink}'>Restore My Account</a>
                <br>
                <p>If you are certain you want to delete your account immediately, click the link below:</p>
                <a href='{permanentDeleteLink}'>Delete My Account Permanently</a>";

            await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody); // Use _emailService
        }
        catch (Exception ex)
        {
            emailError = $"Email sending failed: {ex.Message}"; // More descriptive error
        }

        await HttpContext.SignOutAsync();

        if (emailError != null)
        {
            ViewBag.EmailError = emailError;
        }

        return View("DeletionInitiated");
    }

    // This action is triggered by the link in the email.
    [HttpGet]
    public async Task<IActionResult> RestoreAccount(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest("Invalid token.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.DeletionToken == token && u.IsPendingDeletion);

        if (user != null && user.DeletionRequestDate.Value.AddDays(7) > DateTime.UtcNow)
        {
            // Token is valid and within the grace period, so restore the account
            user.IsPendingDeletion = false;
            user.DeletionRequestDate = null;
            user.DeletionToken = null;
            await _context.SaveChangesAsync();

            return View("AccountRestored"); // A view confirming account restoration
        }

        return View("Error", "Invalid or expired token."); // An error view
    }

    // This action is triggered by the permanent delete link in the email.
    [HttpGet]
    public async Task<IActionResult> PermanentDelete(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest("Invalid token.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.DeletionToken == token && u.IsPendingDeletion);

        if (user != null)
        {
            // IMPORTANT: Handle related data.
            // The database schema shows that a Member has Orders. You cannot delete a member
            // without first handling their orders due to the foreign key constraint.
            var memberOrders = await _context.Orders.Where(o => o.MemberEmail == user.Email).ToListAsync();
            if (memberOrders.Any())
            {
                // First delete order items, then the orders
                foreach (var order in memberOrders)
                {
                    var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToListAsync();
                    _context.OrderItems.RemoveRange(orderItems);
                }
                _context.Orders.RemoveRange(memberOrders);
            }

            // Finally, delete the user
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return View("AccountDeleted"); // A view confirming permanent deletion
        }

        return View("Error", "Invalid or expired token.");
    }
}
