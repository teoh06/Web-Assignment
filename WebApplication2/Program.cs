global using WebApplication2;
global using WebApplication2.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using WebApplication2.Data;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddScoped<Helper>();

builder.Services.AddAuthentication().AddCookie();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddTransient<IEmailService, EmailService>(serviceProvider =>
{
    var smtpSettings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpSettings>>().Value;
    return new EmailService(
        smtpSettings.SmtpServer,
        smtpSettings.SmtpPort,
        smtpSettings.SenderEmail,
        smtpSettings.SenderPassword
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
app.MapDefaultControllerRoute();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-MY"), // Sets default to English (Malaysia)
    SupportedCultures = new[] { new CultureInfo("en-MY"), new CultureInfo("en-US") }, // List of supported cultures
    SupportedUICultures = new[] { new CultureInfo("en-MY"), new CultureInfo("en-US") } // List of supported UI cultures
});
app.Run();
