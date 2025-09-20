using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Quality;
using System; // Make sure this is included for Guid and DateTime
using System.Diagnostics;
using System.Linq; // Make sure this is included for Any and Where
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication2.Models;
using WebApplication2.Services;

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

        // (1) Get user (member, admin, or chef) record based on email (PK)
        var u = db.Members.Find(vm.Email) as User;
        if (u == null)
            u = db.Admins.Find(vm.Email);
        if (u == null)
            u = db.Chefs.Find(vm.Email);

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
        else if (!string.IsNullOrEmpty(u.OtpCode))
        {
            ModelState.AddModelError("", "Your account is not verified yet. Please check your email for the verification code.");
            ViewBag.PendingVerificationEmail = u.Email;
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
                else if (u is Chef)
                    return RedirectToAction("Orders", "Admin");
                else
                    return RedirectToAction("Index", "Home");
            }

        }

        return View(vm);
    }

    // POST: Account/Logout
    [HttpPost]
    public async Task<IActionResult> Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        // Sign out (await to ensure cookie cleared before redirect)
        await hp.SignOutAsync();

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

            // Send OTP and redirect to verification page
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);
            var action = "complete your registration";
            bool sent = await SendOtpEmailAsync(user!, action);
            
            if (sent)
            {
                TempData["Info"] = $"Registration successful! A verification code has been sent to {vm.Email}. Please check your email to complete registration.";
            }
            else
            {
                TempData["Error"] = "Registration successful, but failed to send verification code. You can request a new code on the next page.";
            }

            return RedirectToAction("VerifyOtp", new { email = vm.Email, action = action, returnUrl = Url.Action("Login", "Account") });
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

        // Debug logging for GET method
        System.Diagnostics.Debug.WriteLine($"GET UpdateProfile - User: {User.Identity!.Name}");
        System.Diagnostics.Debug.WriteLine($"Current PhotoURL: '{m.PhotoURL}'");
        System.Diagnostics.Debug.WriteLine($"PhotoURL is null or empty: {string.IsNullOrEmpty(m.PhotoURL)}");
        System.Diagnostics.Debug.WriteLine($"MemberPhotos count: {m.MemberPhotos?.Count ?? 0}");

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

        System.Diagnostics.Debug.WriteLine($"ViewModel PhotoURL: '{vm.PhotoURL}'");
        System.Diagnostics.Debug.WriteLine($"Photo history count: {photoHistory.Count}");
        foreach (var photo in photoHistory)
        {
            System.Diagnostics.Debug.WriteLine($"History photo: {photo.FileName} (ID: {photo.Id})");
        }

        // Check if the photo file actually exists on disk
        if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default.png")
        {
            var photoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "photos", m.PhotoURL);
            var fileExists = System.IO.File.Exists(photoPath);
            System.Diagnostics.Debug.WriteLine($"Photo file path: {photoPath}");
            System.Diagnostics.Debug.WriteLine($"Photo file exists: {fileExists}");
            if (fileExists)
            {
                var fileInfo = new FileInfo(photoPath);
                System.Diagnostics.Debug.WriteLine($"Photo file size: {fileInfo.Length} bytes");
                System.Diagnostics.Debug.WriteLine($"Photo file created: {fileInfo.CreationTime}");
            }
        }

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

        // Debug logging for seeded account
        System.Diagnostics.Debug.WriteLine($"UpdateProfile - User: {User.Identity!.Name}");
        System.Diagnostics.Debug.WriteLine($"Member found: {m != null}, PhotoURL: {m?.PhotoURL}");
        System.Diagnostics.Debug.WriteLine($"MemberPhotos count: {m?.MemberPhotos?.Count ?? 0}");
        System.Diagnostics.Debug.WriteLine($"ProfilePicture: {vm.ProfilePicture != null}");
        System.Diagnostics.Debug.WriteLine($"ProcessedImageData: {!string.IsNullOrEmpty(vm.ProcessedImageData)}");
        System.Diagnostics.Debug.WriteLine($"SelectedPhotoPath: {vm.SelectedPhotoPath}");

        if (vm.ProfilePicture != null)
        {
            var err = hp.ValidatePhoto(vm.ProfilePicture);
            if (err != "") 
            {
                ModelState.AddModelError("ProfilePicture", err);
                System.Diagnostics.Debug.WriteLine($"Photo validation error: {err}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Photo validation passed");
            }
        }

        // --- Fix: Always check if any photo update is requested ---
        bool photoChanged = false;
        bool isNewUpload = false; // true if the new photo comes from file upload or cropper
        string? newPhotoUrl = null;
        string oldPhotoUrl = m.PhotoURL;

        try
        {
            if (!string.IsNullOrEmpty(vm.ProcessedImageData))
            {
                // Case 1: New photo from cropper
                System.Diagnostics.Debug.WriteLine("Processing cropped image data");
                newPhotoUrl = SaveBase64Image(vm.ProcessedImageData, "photos");
                photoChanged = true;
                isNewUpload = true;
                System.Diagnostics.Debug.WriteLine($"Cropped image saved: {newPhotoUrl}");
            }
            else if (vm.ProfilePicture != null)
            {
                // Case 2: New photo from direct upload
                System.Diagnostics.Debug.WriteLine("Processing direct file upload");
                newPhotoUrl = hp.SavePhoto(vm.ProfilePicture, "photos");
                photoChanged = true;
                isNewUpload = true;
                System.Diagnostics.Debug.WriteLine($"Direct upload saved: {newPhotoUrl}");
            }
            else if (!string.IsNullOrEmpty(vm.SelectedPhotoPath) && int.TryParse(vm.SelectedPhotoPath, out int selectedPhotoId))
            {
                // Case 3: Reusing a photo from history
                System.Diagnostics.Debug.WriteLine($"Selecting photo from history: {selectedPhotoId}");
                var selectedPhoto = m.MemberPhotos.FirstOrDefault(p => p.Id == selectedPhotoId);
                if (selectedPhoto != null && selectedPhoto.FileName != oldPhotoUrl)
                {
                    newPhotoUrl = selectedPhoto.FileName;
                    photoChanged = true;
                    System.Diagnostics.Debug.WriteLine($"History photo selected: {newPhotoUrl}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No photo change detected");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Photo processing exception: {ex.Message}");
            ModelState.AddModelError("", "Error processing photo: " + ex.Message);
        }

        System.Diagnostics.Debug.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
        if (!ModelState.IsValid)
        {
            foreach (var error in ModelState)
            {
                System.Diagnostics.Debug.WriteLine($"ModelState Error - Key: {error.Key}, Errors: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
            }
        }

        // Apply photo change even if other non-photo fields are invalid
        // This ensures profile picture updates are not blocked by unrelated validation (e.g., Address formatting)
        bool photoAlreadyApplied = false;
        var profilePicEntry = ModelState.ContainsKey("ProfilePicture") ? ModelState["ProfilePicture"] : null;
        bool hasProfilePicErrors = profilePicEntry != null && profilePicEntry.Errors != null && profilePicEntry.Errors.Count > 0;
        if (photoChanged && !hasProfilePicErrors && newPhotoUrl != null && newPhotoUrl != oldPhotoUrl)
        {
            System.Diagnostics.Debug.WriteLine("Applying photo change regardless of other field validation");
            // Add the previous photo to history if it's not a default one and not already there
            if (!string.IsNullOrEmpty(oldPhotoUrl) && oldPhotoUrl != "default.png" && !m.MemberPhotos.Any(p => p.FileName == oldPhotoUrl))
            {
                System.Diagnostics.Debug.WriteLine($"Adding old photo to history (pre-save path): {oldPhotoUrl}");
                m.MemberPhotos.Add(new MemberPhoto
                {
                    MemberEmail = m.Email,
                    FileName = oldPhotoUrl,
                    UploadDate = DateTime.Now
                });
            }
            // If this is a brand-new upload, also record the new file into history (so it appears in the grid immediately)
            if (isNewUpload && !m.MemberPhotos.Any(p => p.FileName == newPhotoUrl))
            {
                System.Diagnostics.Debug.WriteLine($"Recording new uploaded photo into history (pre-save): {newPhotoUrl}");
                m.MemberPhotos.Add(new MemberPhoto
                {
                    MemberEmail = m.Email,
                    FileName = newPhotoUrl,
                    UploadDate = DateTime.Now
                });
            }
            m.PhotoURL = newPhotoUrl;
            try
            {
                db.SaveChanges();
                photoAlreadyApplied = true;
                System.Diagnostics.Debug.WriteLine("Photo change saved to DB (pre main ModelState block)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving photo change: {ex.Message}");
                ModelState.AddModelError("", "Failed to save profile photo: " + ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            System.Diagnostics.Debug.WriteLine($"Photo changed: {photoChanged}, New URL: {newPhotoUrl}, Old URL: {oldPhotoUrl}");
            
            m.Name = vm.Name;
            m.Address = vm.Address;
            m.PhoneNumber = vm.PhoneNumber;

            // --- If a photo change occurred, update history and current photo ---
            if (!photoAlreadyApplied && photoChanged && newPhotoUrl != null && newPhotoUrl != oldPhotoUrl)
            {
                System.Diagnostics.Debug.WriteLine("Updating photo URL and history");
                // Add the previous photo to history if it's not a default one and not already there
                if (!string.IsNullOrEmpty(oldPhotoUrl) && oldPhotoUrl != "default.png" && !m.MemberPhotos.Any(p => p.FileName == oldPhotoUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"Adding old photo to history: {oldPhotoUrl}");
                    m.MemberPhotos.Add(new MemberPhoto
                    {
                        MemberEmail = m.Email,
                        FileName = oldPhotoUrl,
                        UploadDate = DateTime.Now
                    });
                }
                // Record the new uploaded photo into history if it came from a fresh upload
                if (isNewUpload && !m.MemberPhotos.Any(p => p.FileName == newPhotoUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"Recording new uploaded photo into history: {newPhotoUrl}");
                    m.MemberPhotos.Add(new MemberPhoto
                    {
                        MemberEmail = m.Email,
                        FileName = newPhotoUrl,
                        UploadDate = DateTime.Now
                    });
                }
                m.PhotoURL = newPhotoUrl;
                System.Diagnostics.Debug.WriteLine($"Updated PhotoURL to: {m.PhotoURL}");
            }

            try
            {
                db.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Database changes saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database save error: {ex.Message}");
                ModelState.AddModelError("", "Error saving changes: " + ex.Message);
                goto RepopulateAndReturn;
            }

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

        RepopulateAndReturn:

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

            // Send reset password email
            string subject = "QuickBite - Password Reset Request";
            string body = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                <div style='background: linear-gradient(135deg, #2A1810, #1A202C); padding: 30px; text-align: center; color: white;'>
                    <h1 style='margin: 0; font-size: 28px;'>Password Reset</h1>
                    <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Your QuickBite account password has been reset</p>
                </div>
                <div style='padding: 30px; border: 1px solid #dee2e6; border-top: none;'>
                    <p style='font-size: 16px; color: #333; margin-bottom: 20px;'>Hello <strong>{u.Name}</strong>,</p>
                    <p style='font-size: 16px; color: #333; margin-bottom: 25px;'>A password reset was requested for your account. Please use the temporary password below to log in. For security, change your password after logging in.</p>
                    <div style='background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); border: 2px dashed #6c757d; padding: 25px; text-align: center; margin: 25px 0; border-radius: 8px;'>
                        <p style='margin: 0 0 10px 0; font-size: 14px; color: #6c757d; text-transform: uppercase; letter-spacing: 1px;'>Temporary Password</p>
                        <div style='font-size: 32px, font-weight: bold, letter-spacing: 4px; color: #495057; font-family: monospace;'>
                            {password}
                        </div>
                    </div>
                    <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 0; font-size: 14px; color: #856404;'>
                            <strong>⏰ Important:</strong> Change your password after logging in for maximum security.
                        </p>
                    </div>
                    <p style='font-size: 14px; color: #6c757d; margin-top: 25px;'>
                        If you did not request this reset, please contact our support team immediately.
                    </p>
                    <div style='border-top: 1px solid #dee2e6; padding-top: 20px; margin-top: 25px; text-align: center;'>
                        <p style='margin: 0; font-size: 14px; color: #6c757d;'>
                            Best regards,<br>
                            <strong style='color: #495057;'>The QuickBite Team</strong>
                        </p>
                    </div>
                </div>
            </div>";
        _emailService.SendEmailAsync(u.Email, subject, body);

        TempData["Info"] = "A password reset email has been sent to your registered email address. Please check your inbox.";
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
            string subject = "QuickBite - Complete Your Registration";
            string body = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                <div style='background: linear-gradient(135deg, #2A1810, #1A202C); padding: 30px; text-align: center; color: white;'>
                    <h1 style='margin: 0; font-size: 28px;'>Welcome to QuickBite!</h1>
                    <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Complete your registration</p>
                </div>
                <div style='padding: 30px; border: 1px solid #dee2e6; border-top: none;'>
                    <p style='font-size: 16px; color: #333; margin-bottom: 20px;'>Hello <strong>{user.Name}</strong>,</p>
                    <p style='font-size: 16px; color: #333; margin-bottom: 25px;'>Thank you for registering with QuickBite! To complete your registration, please use the verification code below:</p>
                    
                    <div style='background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); border: 2px dashed #6c757d; padding: 25px; text-align: center; margin: 25px 0; border-radius: 8px;'>
                        <p style='margin: 0 0 10px 0; font-size: 14px; color: #6c757d; text-transform: uppercase; letter-spacing: 1px;'>Your Verification Code</p>
                        <div style='font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #495057; font-family: monospace;'>
                            {otp}
                        </div>
                    </div>
                    
                    <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 0; font-size: 14px; color: #856404;'>
                            <strong>⏰ Important:</strong> This code will expire in 15 minutes for security reasons.
                        </p>
                    </div>
                    
                    <p style='font-size: 14px; color: #6c757d; margin-top: 25px;'>
                        If you didn't create an account with QuickBite, please ignore this email or contact our support team.
                    </p>
                    
                    <div style='border-top: 1px solid #dee2e6; padding-top: 20px; margin-top: 25px; text-align: center;'>
                        <p style='margin: 0; font-size: 14px; color: #6c757d;'>
                            Best regards,<br>
                            <strong style='color: #495057;'>The QuickBite Team</strong>
                        </p>
                    </div>
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
        // Verify the user exists and has a pending OTP
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Register");
        }

        if (string.IsNullOrEmpty(user.OtpCode))
        {
            TempData["Error"] = "No verification code found. Please register again.";
            return RedirectToAction("Register");
        }

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
        
        // For registration verification, redirect to login with success message
        if (vm.Action.Contains("registration"))
        {
            TempData["Info"] = "Account verified successfully! You can now login.";
            return RedirectToAction("Login");
        }
        
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
            <div style='font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:16px;box-shadow:0 4px 24px #0002;overflow:hidden;'>
                <div style='background:linear-gradient(90deg,#48BB78 60%,#38A169 100%);color:#fff;padding:32px 24px;text-align:center;'>
                    <span style='font-size:2.5rem;display:inline-block;margin-bottom:12px;'>✔️</span>
                    <h2 style='margin:0;font-size:1.7rem;font-weight:700;'>Account Modification Request</h2>
                </div>
                <div style='padding:32px 24px;'>
                    <p style='font-size:1.1rem;color:#2D3748;margin-bottom:18px;'>We have received a request to delete your account.</p>
                    <p style='color:#718096;margin-bottom:18px;'>If you did not make this request, please ignore this email.</p>
                    <div style='background:linear-gradient(90deg,#FFF5F2 60%,#F8F9FA 100%);border-radius:8px;padding:18px 16px;margin-bottom:18px;'>
                        <span style='font-size:1.2rem;color:#E53E3E;font-weight:600;'>Your account will be permanently deleted in 7 days.</span>
                        <p style='margin:10px 0 0 0;color:#2D3748;'>If you change your mind, you can restore your account by clicking the button below:</p>
                        <a href='{recoveryLink}' style='display:inline-block;margin:16px 0 0 0;padding:12px 28px;background:#48BB78;color:#fff;font-weight:600;border-radius:8px;text-decoration:none;font-size:1.1rem;box-shadow:0 2px 8px #48BB78;'>Restore My Account</a>
                    </div>
                    <div style='background:linear-gradient(90deg,#FFF5F2 60%,#F8F9FA 100%);border-radius:8px;padding:18px 16px;margin-bottom:18px;'>
                        <span style='font-size:1.2rem;color:#E53E3E;font-weight:600;'>Delete Immediately</span>
                        <p style='margin:10px 0 0 0;color:#2D3748;'>If you are certain you want to delete your account immediately, click the button below:</p>
                        <a href='{permanentDeleteLink}' style='display:inline-block;margin:16px 0 0 0;padding:12px 28px;background:#E53E3E;color:#fff;font-weight:600;border-radius:8px;text-decoration:none;font-size:1.1rem;box-shadow:0 2px 8px #E53E3E;'>Delete My Account Permanently</a>
                    </div>
                    <p style='color:#718096;font-size:0.95rem;margin-top:24px;'>If you have any questions, please contact our support team.</p>
                </div>
            </div>
            ";
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
            // --- Preserve orders for admin sales tracking ---
            var memberOrders = await db.Orders.Where(o => o.MemberEmail == user.Email).ToListAsync();
            foreach (var order in memberOrders)
            {
                order.Member = null; // Remove navigation, keep info fields
            }
            // Remove the user
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

            var mail = new MailMessage
            {
                Subject = "Account Pending Deletion",
                Body = $"<p>Hello {user.Name},</p><p>Your account has been marked for deletion by admin. Please contact admin for more details.</p>",
                IsBodyHtml = true
            };
            mail.To.Add(user.Email);

            hp.SendEmail(mail);

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

            var mail = new MailMessage
            {
                Subject = "Account Restored",
                Body = $"<p>Hello {user.Name},</p><p>Your account has been successfully restored.</p>",
                IsBodyHtml = true
            };
            mail.To.Add(user.Email);

            hp.SendEmail(mail);

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

    // AJAX endpoint to get member information for auto-fill
    [Authorize(Roles = "Member")]
    [HttpGet]
    public async Task<IActionResult> GetMemberInfo()
    {
        try
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            if (member == null)
            {
                return Json(new { success = false, message = "Member not found" });
            }

            return Json(new 
            { 
                success = true, 
                phoneNumber = member.PhoneNumber ?? "",
                address = member.Address ?? "",
                name = member.Name ?? ""
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error retrieving member information" });
        }
    }
}