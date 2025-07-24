global using WebApplication2;
global using WebApplication2.Models;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddScoped<Helper>();

builder.Services.AddAuthentication().AddCookie();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization("en-MY");
app.MapDefaultControllerRoute();
app.Run();
