using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.Services;

using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Data Protection (persist keys locally in workspace)
var keysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("VocaLearn");

// Database configuration
var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString(
    provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ? "SqlServerConnection" : "DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=vocalearn.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Register Application Services
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IVocabularyService, VocabularyService>();
builder.Services.AddScoped<IFlashcardService, FlashcardService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.InitializeAsync(db, userMgr, roleMgr);

        if (args.Contains("--verify"))
        {
            var vocabSvc = services.GetRequiredService<IVocabularyService>();
            var flashSvc = services.GetRequiredService<IFlashcardService>();
            var quizSvc = services.GetRequiredService<IQuizService>();
            var progSvc = services.GetRequiredService<IProgressService>();
            var importSvc = services.GetRequiredService<IExcelImportService>();

            var logs = await SystemVerification.RunAllTestsAsync(db, userMgr, vocabSvc, flashSvc, quizSvc, progSvc, importSvc);
            foreach (var log in logs)
            {
                Console.WriteLine(log);
            }
            Environment.Exit(0);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
        if (args.Contains("--verify"))
        {
            Console.WriteLine($"VERIFICATION ERROR: {ex.Message}\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
