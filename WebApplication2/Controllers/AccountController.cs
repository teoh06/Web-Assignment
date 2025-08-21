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
        // Always ensure SiteKey is available for the view
        ViewBag.SiteKey = HttpContext.RequestServices.GetService<IConfiguration>()?["GoogleReCaptcha:SiteKey"];

        var recaptchaToken = Request.Form["g-recaptcha-response"];
        var siteKey = ViewBag.SiteKey as string;

        // Only validate recaptcha if site key is configured
        if (!string.IsNullOrEmpty(siteKey) && !await _recaptcha.VerifyAsync(recaptchaToken))
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
        var u = db.Members.Find(vm.Email) as User;
        if (u == null)
            u = db.Admins.Find(vm.Email);

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
        // Always ensure SiteKey is available for the view
        ViewBag.SiteKey = HttpContext.RequestServices.GetService<IConfiguration>()?["GoogleReCaptcha:SiteKey"];

        var recaptchaToken = Request.Form["g-recaptcha-response"];
        var siteKey = ViewBag.SiteKey as string;

        // Only validate recaptcha if site key is configured
        if (!string.IsNullOrEmpty(siteKey) && !await _recaptcha.VerifyAsync(recaptchaToken))
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
                PhotoURL = vm.ProfilePicture != null ? hp.SavePhoto(vm.ProfilePicture, "photos") : "default.png",
                Address = vm.Address,
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

        // The photo history should include all unique, non-default photos associated with the member
        var photoHistory = m.MemberPhotos
            .OrderByDescending(p => p.UploadDate)
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
            Address = m.Address,
            PhoneNumber = m.PhoneNumber,
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
            if (err != "") ModelState.AddModelError("ProfilePicture", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;
            m.Address = vm.Address;
            m.PhoneNumber = vm.PhoneNumber;

            string? newPhotoUrl = null;
            string oldPhotoUrl = m.PhotoURL;

            // --- Determine the new photo URL ---
            try
            {
                if (!string.IsNullOrEmpty(vm.ProcessedImageData))
                {
                    // Case 1: New photo from cropper
                    newPhotoUrl = SaveBase64Image(vm.ProcessedImageData, "photos");
                }
                else if (vm.ProfilePicture != null)
                {
                    // Case 2: New photo from direct upload
                    newPhotoUrl = hp.SavePhoto(vm.ProfilePicture, "photos");
                }
                else if (!string.IsNullOrEmpty(vm.SelectedPhotoPath) && int.TryParse(vm.SelectedPhotoPath, out int selectedPhotoId))
                {
                    // Case 3: Reusing a photo from history
                    var selectedPhoto = m.MemberPhotos.FirstOrDefault(p => p.Id == selectedPhotoId);
                    if (selectedPhoto != null)
                    {
                        newPhotoUrl = selectedPhoto.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error processing photo: " + ex.Message);
                // On error, fall through to return the view with the model state error
            }


            // --- If a photo change occurred, update history and current photo ---
            if (newPhotoUrl != null && newPhotoUrl != oldPhotoUrl)
            {
                // Add the previous photo to history if it's not a default one and not already there
                if (!string.IsNullOrEmpty(oldPhotoUrl) && oldPhotoUrl != "default.png" && !m.MemberPhotos.Any(p => p.FileName == oldPhotoUrl))
                {
                    m.MemberPhotos.Add(new MemberPhoto
                    {
                        MemberEmail = m.Email,
                        FileName = oldPhotoUrl,
                        UploadDate = DateTime.Now
                    });
                }

                // Set the new photo
                m.PhotoURL = newPhotoUrl;
            }

            // Save all changes (name, address, new photo URL, new history entry)
            db.SaveChanges();

            // --- Prune photo history to keep the 4 most recent ones AFTER saving changes ---
            var allPhotos = db.MemberPhotos.Where(p => p.MemberEmail == m.Email).ToList();
            if (allPhotos.Count > 4)
            {
                var photosToRemove = allPhotos
                    .OrderByDescending(p => p.UploadDate)
                    .Skip(4)
                    .ToList();

                foreach (var photo in photosToRemove)
                {
                    // Important: Only delete the file if it's not the current profile picture
                    if (photo.FileName != m.PhotoURL)
                    {
                        hp.DeletePhoto(photo.FileName, "photos");
                    }
                }
                db.MemberPhotos.RemoveRange(photosToRemove);
                db.SaveChanges();
            }

            TempData["Info"] = "Profile updated successfully.";
            return RedirectToAction();
        }

        // Repopulate required VM properties if returning to the view due to an error
        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        vm.PhotoHistory = m.MemberPhotos
            .OrderByDescending(p => p.UploadDate)
            .Select(p => new ProfilePhotoVM { Id = p.Id, FileName = p.FileName, UploadDate = p.UploadDate })
            .ToList();

        return View(vm);
    }

    // ACTION TO DELETE A PHOTO FROM HISTORY
    [Authorize(Roles = "Member")]
    [HttpPost]
    public async Task<IActionResult> DeleteMemberPhoto(int id)
    {
        var member = await db.Members
            .Include(m => m.MemberPhotos)
            .FirstOrDefaultAsync(m => m.Email == User.Identity!.Name);

        if (member == null) return Unauthorized();

        var photoToDelete = member.MemberPhotos.FirstOrDefault(p => p.Id == id);
        if (photoToDelete == null) return NotFound(new { success = false, message = "Photo not found in your history." });

        if (photoToDelete.FileName == member.PhotoURL)
        {
            return BadRequest(new { success = false, message = "Cannot delete the currently active profile photo." });
        }

        try
        {
            // Delete the physical file first
            hp.DeletePhoto(photoToDelete.FileName, "photos");

            // Remove the record from the database
            db.MemberPhotos.Remove(photoToDelete);
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Photo deleted successfully." });
        }
        catch (Exception ex)
        {
            // Log the exception ex
            return StatusCode(500, new { success = false, message = "An error occurred while deleting the photo." });
        }
    }


    // Helper method to save base64 image
    private string SaveBase64Image(string base64Data, string folder)
    {
        try
        {
            // Remove data:image/jpeg;base64, prefix if present
            if (base64Data.Contains(","))
            {
                base64Data = base64Data.Split(',')[1];
            }

            // Convert base64 to byte array
            byte[] imageBytes = Convert.FromBase64String(base64Data);

            // Generate unique filename
            string fileName = Guid.NewGuid().ToString("n") + ".jpg";
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder);

            // Ensure directory exists
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string filePath = Path.Combine(uploadsFolder, fileName);

            // Save the file
            System.IO.File.WriteAllBytes(filePath, imageBytes);

            return fileName;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save processed image: " + ex.Message);
        }
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

    // Add these methods to your AccountController
    
    // Helper method to generate OTP
    private string GenerateOtp()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }
    
    // Helper method to send OTP
    private async Task<bool> SendOtpEmailAsync(User user, string action)
    {
        try
        {
            string otp = GenerateOtp();
            
            // Save OTP to user record
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(15); // OTP valid for 15 minutes
            await db.SaveChangesAsync();
            
            // Send OTP email
            string subject = "Your Verification Code";
            string body = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background-color: #f8f9fa; padding: 20px; text-align: center;'>
                    <h2 style='color: #343a40;'>Verification Required</h2>
                </div>
                <div style='padding: 20px; border: 1px solid #dee2e6; border-top: none;'>
                    <p>Hello {user.Name},</p>
                    <p>You've requested to {action}. Please use the following verification code to complete this action:</p>
                    <div style='background-color: #e9ecef; padding: 15px; text-align: center; font-size: 24px; letter-spacing: 5px; font-weight: bold; margin: 20px 0;'>
                        {otp}
                    </div>
                    <p>This code will expire in 15 minutes.</p>
                    <p>If you didn't request this action, please ignore this email or contact support if you have concerns.</p>
                    <p>Thank you,<br>QuickBite Team</p>
                </div>
            </div>";
            
            await _emailService.SendEmailAsync(user.Email, subject, body);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    // GET: Account/VerifyOtp
    public IActionResult VerifyOtp(string email, string action, string returnUrl)
    {
        var vm = new OtpVerificationVM
        {
            Email = email,
            Action = action,
            ReturnUrl = returnUrl
        };
        return View(vm);
    }
    
    // POST: Account/VerifyOtp
    [HttpPost]
    public async Task<IActionResult> VerifyOtp(OtpVerificationVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }
        
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View(vm);
        }
        
        if (user.OtpCode != vm.OtpCode || !user.OtpExpiry.HasValue || user.OtpExpiry.Value < DateTime.UtcNow)
        {
            ModelState.AddModelError("", "Invalid or expired OTP code.");
            return View(vm);
        }
        
        // Clear OTP after successful verification
        user.OtpCode = null;
        user.OtpExpiry = null;
        await db.SaveChangesAsync();
        
        // Redirect to the appropriate action based on the verification purpose
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
        {
            return Redirect(vm.ReturnUrl);
        }
        
        // Default redirect if returnUrl is not valid
        return RedirectToAction("Index", "Home");
    }
    
    // Request OTP for sensitive actions
    [HttpGet] // Add HttpGet support
    public async Task<IActionResult> RequestOtp(string email, string action, string returnUrl)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound();
        }
        
        bool sent = await SendOtpEmailAsync(user, action);
        if (!sent)
        {
            TempData["Error"] = "Failed to send OTP email. Please try again.";
            return RedirectToAction("Index", "Home");
        }
        
        return RedirectToAction("VerifyOtp", new { email, action, returnUrl });
    }
    
    // Keep the existing POST method as well
    [HttpPost]
    public async Task<IActionResult> RequestOtpPost(string email, string action, string returnUrl)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound();
        }
        
        bool sent = await SendOtpEmailAsync(user, action);
        if (!sent)
        {
            return StatusCode(500, new { success = false, message = "Failed to send OTP email." });
        }
        
        return RedirectToAction("VerifyOtp", new { email, action, returnUrl });
    }

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
        return RedirectToAction("", "Admin");
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
        return RedirectToAction("", "Admin");
    }

    // Address validation API endpoints
    [HttpPost]
    public async Task<JsonResult> ValidateAddress([FromBody] string address)
    {
        try
        {
            var addressService = HttpContext.RequestServices.GetService<Services.IAddressService>();

            if (addressService == null)
            {
                return Json(new
                {
                    isValid = false,
                    errors = new[] { "Address validation service unavailable" }
                });
            }

            var result = await addressService.ValidateAddressAsync(address);

            return Json(new
            {
                isValid = result.IsValid,
                errors = result.Errors,
                warnings = result.Warnings,
                formattedAddress = result.FormattedAddress,
                isGeocodingValidated = result.IsGeocodingValidated
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                isValid = false,
                errors = new[] { "Address validation failed: " + ex.Message }
            });
        }
    }

    [HttpGet]
    public JsonResult GetAddressSuggestions(string partialAddress)
    {
        try
        {
            var addressService = HttpContext.RequestServices.GetService<Services.IAddressService>();

            if (addressService == null || string.IsNullOrWhiteSpace(partialAddress))
            {
                return Json(new string[0]);
            }

            var suggestions = addressService.GetAddressSuggestions(partialAddress);
            return Json(suggestions);
        }
        catch
        {
            return Json(new string[0]);
        }
    }

    [Authorize]
    [HttpGet]
    public IActionResult GetFavoriteListPartial()
    {
        return PartialView("GetFavoriteListPartial");
    }
}