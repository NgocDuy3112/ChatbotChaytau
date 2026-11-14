using aida.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace aida.Extensions;

public static class IdentityExtensions
{
    public static IServiceCollection AddAppIdentity(
        this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(RequireConnectionString(config), npgsql => npgsql.UseVector()));

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var googleId = config["Authentication:Google:ClientId"];
        var googleSecret = config["Authentication:Google:ClientSecret"];
        var googleConfigured = !string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret);

        // Fails at boot in production; dev bypass keeps working with empty creds.
        if (!env.IsDevelopment() && !googleConfigured)
        {
            throw new InvalidOperationException(
                "Missing Google OAuth configuration: Authentication__Google__ClientId / __ClientSecret");
        }

        var auth = services.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        auth.AddIdentityCookies();

        // Registering the scheme with empty credentials throws during OAuthOptions.Validate()
        // the first time authentication runs, so only register it when it is usable.
        if (googleConfigured)
        {
            auth.AddGoogle("Google", o =>
            {
                o.ClientId = googleId!;
                o.ClientSecret = googleSecret!;
                o.CallbackPath = "/signin-google";
            });
        }

        services.AddAuthorization();
        services.AddCascadingAuthenticationState();

        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/login";
            o.AccessDeniedPath = "/login";
            o.ExpireTimeSpan = TimeSpan.FromDays(14);
            o.SlidingExpiration = true;
        });

        return services;
    }

    /// <summary>True when both Google client id and secret are configured.</summary>
    public static bool IsGoogleConfigured(IConfiguration config)
        => !string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"])
           && !string.IsNullOrWhiteSpace(config["Authentication:Google:ClientSecret"]);

    private static string RequireConnectionString(IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres");
        return string.IsNullOrWhiteSpace(cs)
            ? throw new InvalidOperationException("Missing configuration: ConnectionStrings__Postgres")
            : cs;
    }
}