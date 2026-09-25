using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// LocalDB is for developer workstations only. A production/staging deployment must
// provide a real SQL Server connection string through environment/secret configuration.
if (!builder.Environment.IsDevelopment() &&
    connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "LocalDB cannot be used outside Development. Configure ConnectionStrings__DefaultConnection securely on the server.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "MasterFeed.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});


builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "MasterFeed.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Permission/role changes take effect on the next request after the security stamp is updated.
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.AddMasterFeedAuthorization();
builder.Services.AddScoped<UserPermissionService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<CreditService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages(options =>
{
    // Secure-by-default: every future business page requires authentication unless explicitly allowed.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'; object-src 'none'; base-uri 'self'";
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isStaticAsset =
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/icons/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/offline.html", StringComparison.OrdinalIgnoreCase);

        if (!isStaticAsset)
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";

            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var currentUser = await userManager.GetUserAsync(context.User);

            if (currentUser is not null)
            {
                if (!currentUser.IsActive)
                {

                    var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
                    await signInManager.SignOutAsync();
                    context.Response.Redirect("/Account/Login?disabled=1");
                    return;
                }

                var isPasswordChangePath =
                    path.StartsWith("/Account/ChangePassword", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);

                if (currentUser.MustChangePassword && !isPasswordChangePath)
                {
                    context.Response.Redirect("/Account/ChangePassword");
                    return;
                }
            }
        }
    }

    await next();
});


app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Lightweight deployment/monitoring probe. It deliberately returns no connection
// details or other sensitive diagnostics. HTTP 200 means the app can reach its DB.
app.MapGet("/health", async (ApplicationDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "Healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Health check failed while testing database connectivity.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

try
{
    await IdentitySeeder.SeedAsync(app.Services, builder.Configuration, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Identity seeding failed. If this is the first run, create/apply the EF Core migration before starting the app.");
    throw;
}

app.Run();
