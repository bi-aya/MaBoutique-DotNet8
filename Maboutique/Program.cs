using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Maboutique.Data;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
var builder = WebApplication.CreateBuilder(args);

// AJOUTER REDIS ICI
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379"; // L'adresse de votre serveur Redis
    options.InstanceName = "MaBoutique_"; // Préfixe pour vos clés
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<MaboutiqueContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MaboutiqueContext") ?? throw new InvalidOperationException("Connection string 'MaboutiqueContext' not found.")));

// LE SERVICE IDENTITY 
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // Pas besoin de confirmer l'email pour le test
})  .AddRoles<IdentityRole>() //POUR LES ROLES
    .AddEntityFrameworkStores<MaboutiqueContext>();

// Ajouter le support de Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Injection du Service IA
builder.Services.AddScoped<Maboutique.Services.OpenAIService>();

var app = builder.Build();

// --- DÉBUT CONFIGURATION DIRHAM ---
var cultureInfo = new CultureInfo("fr-MA"); // Culture Marocaine
cultureInfo.NumberFormat.CurrencySymbol = "DH"; // On force le symbole "DH"
cultureInfo.NumberFormat.CurrencyDecimalDigits = 2; // 2 chiffres après la virgule

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultureInfo),
    SupportedCultures = new List<CultureInfo> { cultureInfo },
    SupportedUICultures = new List<CultureInfo> { cultureInfo }
};

app.UseRequestLocalization(localizationOptions);
// --- FIN CONFIGURATION DIRHAM ---

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

//Activer la Session AVANT l'Authorization
app.UseSession();
//ACTIVER L'AUTHENTIFICATION
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// --- DÉBUT BLOC CRÉATION ADMIN ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        // 1. Créer les rôles s'ils n'existent pas
        string[] roleNames = { "Admin", "User" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Créer l'utilisateur Admin par défaut
        string adminEmail = "admin@maboutique.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            // Mot de passe fort obligatoire (Majuscule, chiffre, caractère spécial)
            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erreur lors de la création des rôles.");
    }
}
// --- FIN BLOC CRÉATION ADMIN ---

app.Run();
