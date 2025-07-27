global using Demo;
global using Demo.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Demo.Data;
using Demo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddScoped<Helper>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ClaimsIssuer = "MyApp";
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();


builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailService, EmailService>(serviceProvider =>
{
    var smtpSettings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpSettings>>().Value;
    return new EmailService(
        smtpSettings.Host,
        smtpSettings.Port,
        smtpSettings.User,
        smtpSettings.Pass
    );
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-MY"), // Sets default to English (Malaysia)
    SupportedCultures = new[] { new CultureInfo("en-MY"), new CultureInfo("en-US") }, // List of supported cultures
    SupportedUICultures = new[] { new CultureInfo("en-MY"), new CultureInfo("en-US") } // List of supported UI cultures
});
app.Run();
