using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace WebApplication2;

public class Helper
{
    private readonly IWebHostEnvironment en;
    private readonly IHttpContextAccessor ct;
    private readonly IConfiguration cf;

    public Helper(IWebHostEnvironment en,
                  IHttpContextAccessor ct,
                  IConfiguration cf)
    {
        this.en = en;
        this.ct = ct;
        this.cf = cf;
    }

    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photo is allowed.";
        }
        else if (f.Length > 1 * 1024 * 1024)
        {
            return "Photo size cannot more than 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
        img.Save(path);

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        File.Delete(path);
    }



    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
               == PasswordVerificationResult.Success;
    }

    public void SignIn(string email, string role, bool rememberMe)
    {

        List<Claim> claims =
        [
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, "Cookies");
        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
        };

        ct.HttpContext!.SignInAsync("Cookies", principal, properties);
    }


    public async Task SignOutAsync()
    {
        await ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }

        return password;
    }



    // ------------------------------------------------------------------------
    // Email Helper Functions
    // ------------------------------------------------------------------------

    public void SendEmail(MailMessage mail)
    {
        try
        {
            string user = cf["Smtp:User"] ?? "";
            string pass = cf["Smtp:Pass"] ?? "";
            string name = cf["Smtp:Name"] ?? "";
            string host = cf["Smtp:Host"] ?? "";
            int port = cf.GetValue<int>("Smtp:Port");

            // Validate SMTP settings
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(host))
                throw new InvalidOperationException("SMTP settings are not properly configured.");

            mail.From = new MailAddress(user, name);
            mail.ReplyToList.Add(new MailAddress(user, name));
            
            // Add essential email headers
            mail.Headers.Add("X-Priority", "1");
            mail.Headers.Add("X-MSMail-Priority", "High");
            mail.Headers.Add("Importance", "High");
            mail.Headers.Add("X-Mailer", "Microsoft ASP.NET");
            
            mail.IsBodyHtml = true;
            
            // Set content type for HTML email
            var htmlView = AlternateView.CreateAlternateViewFromString(mail.Body, null, "text/html");
            mail.AlternateViews.Clear();
            mail.AlternateViews.Add(htmlView);
            
            // Add plain text version
            var plainText = System.Text.RegularExpressions.Regex.Replace(mail.Body, "<[^>]+>", string.Empty);
            var plainView = AlternateView.CreateAlternateViewFromString(plainText, null, "text/plain");
            mail.AlternateViews.Add(plainView);

            using var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, pass),
                Timeout = 30000 // 30 seconds timeout
            };

            smtp.Send(mail);
        }
        catch (SmtpException ex)
        {
            // Provide detailed SMTP error information
            var error = $"SMTP Error: {ex.Message}";
            if (ex.InnerException != null)
                error += $"\nInner Error: {ex.InnerException.Message}";
            
            throw new Exception($"Failed to send email. {error}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Email sending failed: {ex.Message}", ex);
        }
    }



    // ------------------------------------------------------------------------
    // DateTime Helper Functions
    // ------------------------------------------------------------------------

    // Return January (1) to December (12)
    public SelectList GetMonthList()
    {
        var list = new List<object>();

        for (int n = 1; n <= 12; n++)
        {
            list.Add(new
            {
                Id = n,
                Name = new DateTime(1, n, 1).ToString("MMMM"),
            });
        }

        return new SelectList(list, "Id", "Name");
    }

    // Return min to max years
    public SelectList GetYearList(int min, int max, bool reverse = false)
    {
        var list = new List<int>();

        for (int n = min; n <= max; n++)
        {
            list.Add(n);
        }

        if (reverse) list.Reverse();

        return new SelectList(list);
    }
}
