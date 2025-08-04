using WebApplication2.Models;
using WebApplication2.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; // Make sure this is included for Guid and DateTime
using System.Diagnostics;
using System.Linq; // Make sure this is included for Any and Where
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace WebApplication2.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly Helper hp;
    private readonly IEmailService _emailService;
    private readonly RecaptchaHelper _recaptcha;

    private const int MaxFailedAttempts = 3;
    private const int BlockMinutes = 5;

    public AccountController(DB db, Helper hp, IEmailService emailService, RecaptchaHelper recaptcha)
    {
        this.db = db;
        this.hp = hp;
        this._emailService = emailService;
        this._recaptcha = recaptcha;
    }

    // GET: Account/Login
    public IActionResult Login()
    {
        ViewBag.SiteKey = HttpContext.RequestServices.GetService<IConfiguration>()?["GoogleReCaptcha:SiteKey"];
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? returnURL)
    {
        var recaptchaToken = Request.Form["g-recaptcha-response"];
        if (!await _recaptcha.VerifyAsync(recaptchaToken))
        {
            ModelState.AddModelError("", "reCAPTCHA validation failed. Please try again.");
        }

        // --- Temporary login blocking logic ---
        string failKey = $"LoginFail_{vm.Email}";
        string blockKey = $"LoginBlock_{vm.Email}";
        int failCount = HttpContext.Session.GetInt32(failKey) ?? 0;
        DateTime? blockUntil = null;
        var blockUntilStr = HttpContext.Session.GetString(blockKey);
        if (!string.IsNullOrEmpty(blockUntilStr) && DateTime.TryParse(blockUntilStr, out var dt))
            blockUntil = dt;

        if (blockUntil.HasValue && blockUntil.Value > DateTime.UtcNow)
        {
            var mins = (int)(blockUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
            ModelState.AddModelError("", $"Too many failed attempts. Login blocked for {mins} more minute(s).");
            return View(vm);
        }

        // (1) Get user (admin or member) record based on email (PK)
        var u = db.Users.Find(vm.Email);

        // (2) Custom validation -> verify password
        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            failCount++;
            HttpContext.Session.SetInt32(failKey, failCount);
            if (failCount >= MaxFailedAttempts)
            {
                var until = DateTime.UtcNow.AddMinutes(BlockMinutes);
                HttpContext.Session.SetString(blockKey, until.ToString("o"));
                ModelState.AddModelError("", $"Too many failed attempts. Login blocked for {BlockMinutes} minutes.");
                return View(vm);
            }
            ModelState.AddModelError("", $"Login credentials not matched. ({failCount}/{MaxFailedAttempts})");
        }
        else if (u.IsPendingDeletion) // Add this condition
        {
            ModelState.AddModelError("", "This account is pending deletion. Please check your email for restoration options.");
        }

        if (ModelState.IsValid)
        {
            TempData["Info"] = "Login successfully.";
            // --- Enhanced Remember Me: set persistent cookie ---
            var claims = new List<Claim> {
                new Claim(ClaimTypes.Name, u.Email),
                new Claim(ClaimTypes.Role, u.Role)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = vm.RememberMe,
                ExpiresUtc = vm.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : (DateTimeOffset?)null
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
            // Reset fail count on success
            HttpContext.Session.Remove(failKey);
            HttpContext.Session.Remove(blockKey);
            if (string.IsNullOrEmpty(returnURL))
            {
                if (u is Admin)
                    return RedirectToAction("Index", "Admin");
                else
                    return RedirectToAction("Index", "Home");
            }

        }

        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        // Sign out
        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        ViewBag.SiteKey = HttpContext.RequestServices.GetService<IConfiguration>()?["GoogleReCaptcha:SiteKey"];
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        var recaptchaToken = Request.Form["g-recaptcha-response"];
        if (!await _recaptcha.VerifyAsync(recaptchaToken))
        {
            ModelState.AddModelError("", "reCAPTCHA validation failed. Please try again.");
        }

        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("Photo") && vm.ProfilePicture != null)
        {
            var err = hp.ValidatePhoto(vm.ProfilePicture);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            // Insert member
            db.Members.Add(new Member
            {
                Email = vm.Email,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = vm.ProfilePicture != null ? hp.SavePhoto(vm.ProfilePicture, "photos") : "default.png", // Use default if not provided
                DeletionToken = "",
            });
            db.SaveChanges();

            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        // Get user (admin or member) record based on email (PK)
        // TODO
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        // If current password not matched
        // TODO
        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            // Update user password (hash)
            u.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    public IActionResult UpdateProfile()
    {
        var m = db.Members.Include(x => x.MemberPhotos)
                      .FirstOrDefault(x => x.Email == User.Identity!.Name);
    if (m == null) return RedirectToAction("Index", "Home");

    // Get up to 4 most recent previous photos
    var photoHistory = m.MemberPhotos
        .OrderByDescending(p => p.UploadDate)
        .Take(4)
        .Select(p => new ProfilePhotoVM
        {
            Id = p.Id,
            FileName = p.FileName,
            UploadDate = p.UploadDate
        })
        .ToList();

    var vm = new UpdateProfileVM
    {
        Email = m.Email,
        Name = m.Name,
        PhotoURL = m.PhotoURL,
        PhotoHistory = photoHistory
    };

    return View(vm);
}

    // POST: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var m = db.Members.Include(x => x.MemberPhotos)
                          .FirstOrDefault(x => x.Email == User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.ProfilePicture != null)
        {
            var err = hp.ValidatePhoto(vm.ProfilePicture);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            // Handle new photo upload
            if (vm.ProfilePicture != null)
            {
                // Save current photo to history if it exists and is not default
                if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default.jpg")
                {
                    // Only add to history if not already in history
                    if (!m.MemberPhotos.Any(p => p.FileName == m.PhotoURL))
                    {
                        m.MemberPhotos.Add(new MemberPhoto
                        {
                            MemberEmail = m.Email,
                            FileName = m.PhotoURL,
                            UploadDate = DateTime.Now
                        });
                    }
                    // Keep only the 4 most recent previous photos
                    var toRemove = m.MemberPhotos
                        .OrderByDescending(p => p.UploadDate)
                        .Skip(4)
                        .ToList();
                    db.MemberPhotos.RemoveRange(toRemove);
                }
                hp.DeletePhoto(m.PhotoURL, "photos");
                m.PhotoURL = hp.SavePhoto(vm.ProfilePicture, "photos");
            }
            // Handle selecting a previous photo
            else if (!string.IsNullOrEmpty(Request.Form["SelectedPhotoPath"]))
            {
                var selectedPhotoIdStr = Request.Form["SelectedPhotoPath"].ToString();
                if (int.TryParse(selectedPhotoIdStr, out int selectedPhotoId))
                {
                    var selectedPhoto = m.MemberPhotos.FirstOrDefault(p => p.Id == selectedPhotoId);
                    if (selectedPhoto != null)
                    {
                        // Save current photo to history if not already in history and not default
                        if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default.jpg" && !m.MemberPhotos.Any(p => p.FileName == m.PhotoURL))
                        {
                            m.MemberPhotos.Add(new MemberPhoto
                            {
                                MemberEmail = m.Email,
                                FileName = m.PhotoURL,
                                UploadDate = DateTime.Now
                            });
                        }
                        m.PhotoURL = selectedPhoto.FileName;
                    }
                }
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        // Repopulate photo history for redisplay
        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        vm.PhotoHistory = m.MemberPhotos
            .OrderByDescending(p => p.UploadDate)
            .Take(4)
            .Select(p => new ProfilePhotoVM
            {
                Id = p.Id,
                FileName = p.FileName,
                UploadDate = p.UploadDate
            })
            .ToList();
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        var u = db.Users.Find(vm.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            // Generate random password
            string password = hp.RandomPassword();

            // Update user (admin or member) record
            u!.Hash = hp.HashPassword(password);
            db.SaveChanges();

            // TODO: Send reset password email - Nwxt practical (Practical 8)

            TempData["Info"] = $"Password reset to <b>{password}</b>.";
            return RedirectToAction();
        }

        return View();
    }

    // In your AccountController.cs

    // This action is called when the user clicks the "Delete Your Account" button.
    [HttpPost]
    public async Task<IActionResult> InitiateAccountDeletion()
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == User.Identity.Name);
        if (user == null)
        {
            return Unauthorized();
        }

        user.IsPendingDeletion = true;
        user.DeletionRequestDate = DateTime.UtcNow;
        user.DeletionToken = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();

        // --- Send a confirmation email using the INJECTED EmailService ---
        string emailError = null;
        try
        {
            // Use the injected _emailService directly
            var recoveryLink = Url.Action("RestoreAccount", "Account", new { token = user.DeletionToken }, Request.Scheme);
            var permanentDeleteLink = Url.Action("PermanentDelete", "Account", new { token = user.DeletionToken }, Request.Scheme);

            string emailSubject = "Account Modification Request";
            string emailBody = $@"
            <div style='font-family: Georgia, serif; font-size: 16px; color: #000; line-height: 1.8;'>
                <p>We have received a request to delete your account.</p>
                <p>If you did not make this request, please ignore this email.</p>
                <p>Your account will be permanently deleted in 7 days. If you change your mind, you can restore your account by clicking the link below:</p>
                <a href='{recoveryLink}' style='color: #0066cc; font-weight: bold; text-decoration: underline;'>Restore My Account</a>
                <br><br>
                <p>If you are certain you want to delete your account immediately, click the link below:</p>
                <a href='{permanentDeleteLink}' style='color: #cc0000; font-weight: bold; text-decoration: underline;'>Delete My Account Permanently</a>
            </div>";

            await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody); // Use _emailService
        }
        catch (Exception ex)
        {
            emailError = ex.Message;
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

        var user = await db.Users.FirstOrDefaultAsync(u => u.DeletionToken == token && u.IsPendingDeletion);

        if (user != null && user.DeletionRequestDate.Value.AddDays(7) > DateTime.UtcNow)
        {
            // Token is valid and within the grace period, so restore the account
            user.IsPendingDeletion = false;
            user.DeletionRequestDate = null;
            user.DeletionToken = null;
            await db.SaveChangesAsync();

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

        var user = await db.Users.FirstOrDefaultAsync(u => u.DeletionToken == token && u.IsPendingDeletion);

        if (user != null)
        {
            // IMPORTANT: Handle related data.
            // The database schema shows that a Member has Orders. You cannot delete a member
            // without first handling their orders due to the foreign key constraint.
            var memberOrders = await db.Orders.Where(o => o.MemberEmail == user.Email).ToListAsync();
            if (memberOrders.Any())
            {
                // First delete order items, then the orders
                foreach (var order in memberOrders)
                {
                    var orderItems = await db.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToListAsync();
                    db.OrderItems.RemoveRange(orderItems);
                }
                db.Orders.RemoveRange(memberOrders);
            }

            // Finally, delete the user
            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return View("AccountDeleted"); // A view confirming permanent deletion
        }

        return View("Error", "Invalid or expired token.");
    }

    [HttpPost]
    public IActionResult Delete(string email)
    {
        var user = db.Users.Find(email);
        if (user != null && !user.IsPendingDeletion)
        {
            user.IsPendingDeletion = true;
            user.DeletionRequestDate = DateTime.Now;
            user.DeletionToken = Guid.NewGuid().ToString("n");
            db.SaveChanges();

            TempData["Info"] = "User marked for deletion.";
        }
        return RedirectToAction("ManageUsers", "Admin");
    }

    [HttpPost]
    public IActionResult Restore(string token)
    {
        var user = db.Users.FirstOrDefault(u => u.DeletionToken == token);
        if (user != null && user.IsPendingDeletion)
        {
            user.IsPendingDeletion = false;
            user.DeletionRequestDate = null;
            user.DeletionToken = null;
            db.SaveChanges();

            TempData["Info"] = "User restored.";
        }
        return RedirectToAction("ManageUsers", "Admin");
    }


}