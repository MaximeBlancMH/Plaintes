using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plaintes.Data;
using Plaintes.Services;
using Plaintes.Services.Export;
using Plaintes.Services.IServices;
using Plaintes.Services.Stockage;
using Serilog;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IUtilisateurSrv, UtilisateurSrv>();
builder.Services.AddScoped<IPlainteSrv, PlainteSrv>();
// MultipartBodyLengthLimit s'applique à chaque fichier d'un envoi : on le règle sur Storage:MaxSize (plus une marge) ; la taille totale
// est relevée uniquement pour la saisie par LimiteEnvoiPiecesFilter.
builder.Services.AddOptions<FormOptions>().Configure<IOptions<StorageSettings>>((o, s) => o.MultipartBodyLengthLimit = s.Value.MaxSize + 1024 * 1024);
builder.Services.AddScoped<LimiteEnvoiPiecesFilter>();

builder.Host.UseSerilog((ctx, lc) => lc.MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.File("Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    );

builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IPieceStorage, FileSystemPieceStorage>();

builder.Services.Configure<ExportSettings>(builder.Configuration.GetSection("Export"));
// Singleton : le verrou qui sérialise les conversions doit être partagé par toutes les requêtes.
builder.Services.AddSingleton<IConvertisseurPdf, LibreOfficeConvertisseurPdf>();
builder.Services.AddScoped<IRecuperateurPieces, RecuperateurPieces>();
builder.Services.AddScoped<IAssembleurPdf, PdfSharpAssembleur>();
builder.Services.AddScoped<IExportPlainteSrv, ExportPlainteSrv>();

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = Saml2Defaults.Scheme;
})
.AddCookie(o =>
{
    o.Events.OnSigningIn = async ctx =>
    {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var userSrv = ctx.HttpContext.RequestServices.GetRequiredService<IUtilisateurSrv>();
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        var email = ctx.Principal!.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
                   ?? ctx.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var user = email == null ? null : await db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserEmail == email);

        var identity = new ClaimsIdentity(ctx.Scheme.Name);

        if (!ComptesClaims.EstActif(user))
        {
            logger.LogWarning("Connexion refusée : aucun utilisateur actif pour l'identifiant {Email} (inconnu ou supprimé)", email);
            identity.AddClaim(new Claim(ComptesClaims.Refuse, "1"));
        }
        else
        {
            var name = ctx.Principal!.FindFirstValue("http://schemas.microsoft.com/identity/claims/displayname");
            if (user.NomUser == null)
                await userSrv.UpdateNameUser(user.IdUser, name);
            identity.AddClaim(new Claim(ClaimTypes.Name, name));
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Access));
            if (!string.IsNullOrEmpty(user.UserEmail))
                identity.AddClaim(new Claim(ClaimTypes.Email, user.UserEmail));
            logger.LogInformation("Connexion de {Nomuser}", user.NomUser);
        }

        // On conserve les claims SAML nécessaires à la déconnexion Microsoft
        foreach (var c in ctx.Principal!.Claims.Where(c =>
                     c.Type == Saml2ClaimTypes.LogoutNameIdentifier || c.Type == Saml2ClaimTypes.SessionIndex))
            identity.AddClaim(c);

        ctx.Principal = new ClaimsPrincipal(identity);
    };
})
.AddSaml2(opt =>
{
    string url = builder.Configuration["Kestrel:Endpoints:Https:Url"];
    opt.SPOptions.EntityId = new EntityId($"{url}/saml2");

    string idp = builder.Configuration["Saml:IdpEntityId"];

    // configuration du fournisseur d'identité
    opt.IdentityProviders.Add(new IdentityProvider(
        new EntityId(builder.Configuration["Saml:IdpEntityId"]),
        opt.SPOptions)
    {
        MetadataLocation = builder.Configuration["Saml:MetadataUrl"],
        LoadMetadata = true
    });

    // comportement de signature des AuthnRequest (SPOptions)
    opt.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Never;
    var path = Path.Combine(builder.Environment.ContentRootPath, "Sustainsys.Saml2.Tests2.pfx");
    opt.SPOptions.ServiceCertificates.Add(new X509Certificate2(path));
});

// Tout [Authorize] exige d'être connecté ET de ne pas être un compte refusé (échec : /Account/AccessDenied -> page « accès refusé »).
builder.Services.AddAuthorization(o => o.DefaultPolicy = ComptesClaims.PolitiqueParDefaut());

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

try
{
    var storage = app.Services.GetRequiredService<IPieceStorage>();
    await storage.CheckAsync();
    app.Logger.LogInformation("Stockage des pièces jointes opérationnel");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Stockage des pièces jointes inutilisable");
    throw;      // l'appli refuse de démarrer
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Dossiers}/{id?}")
    .WithStaticAssets();

app.Run();
