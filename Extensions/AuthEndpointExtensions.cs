using System.Security.Claims;
using chatbotchaytau.Data;
using Microsoft.AspNetCore.Identity;

namespace chatbotchaytau.Extensions;

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IHostEnvironment env)
    {
        app.MapGet("/account/external-login",
            (string? provider, string? returnUrl, SignInManager<AppUser> signInManager, IConfiguration config) =>
        {
            if (!string.Equals(provider ?? "Google", "Google", StringComparison.OrdinalIgnoreCase))
                return Results.Redirect("/login?error=provider");

            if (!IdentityExtensions.IsGoogleConfigured(config))
                return Results.Redirect("/login?error=notconfigured");

            var redirect =
                $"/account/external-login-callback?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}";
            return Results.Challenge(
                signInManager.ConfigureExternalAuthenticationProperties("Google", redirect),
                ["Google"]);
        }).AllowAnonymous();

        app.MapGet("/account/external-login-callback", async (
            string? returnUrl, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager, IConfiguration config) =>
        {
            var target = SafeReturnUrl(returnUrl);

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/login?error=external");

            var signIn = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (signIn.Succeeded)
            {
                // Returning user: refresh profile data and re-assert admin
                // (idempotent — runs on every login).
                var returning = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (returning is not null)
                {
                    await SyncExternalProfileAsync(userManager, info, returning);
                    await EnsureAdminAsync(userManager, roleManager, config, returning);
                }
                return Results.Redirect(target);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var verified = string.Equals(info.Principal.FindFirstValue("email_verified"), "true",
                                         StringComparison.OrdinalIgnoreCase);
            if (email is null || !verified) return Results.Redirect("/login?error=email");

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
                    AvatarUrl = info.Principal.FindFirstValue("picture"),
                };
                var created = await userManager.CreateAsync(user);
                if (!created.Succeeded) return Results.Redirect("/login?error=create");
            }
            else if (!await userManager.IsEmailConfirmedAsync(user))
            {
                return Results.Redirect("/login?error=unconfirmed");
            }

            var linked = await userManager.AddLoginAsync(user, info);
            if (!linked.Succeeded) return Results.Redirect("/login?error=link");

            await SyncExternalProfileAsync(userManager, info, user);
            await EnsureAdminAsync(userManager, roleManager, config, user);

            await signInManager.SignInAsync(user, isPersistent: true);
            return Results.Redirect(target);
        }).AllowAnonymous();

        app.MapPost("/account/signout", async (SignInManager<AppUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        }).RequireAuthorization();

        if (env.IsDevelopment())
        {
            app.MapGet("/dev-login", async (
                SignInManager<AppUser> signInManager, UserManager<AppUser> userManager,
                RoleManager<IdentityRole> roleManager) =>
            {
                const string email = "dev@localhost";
                var user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    user = new AppUser
                    {
                        UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Dev User"
                    };
                    await userManager.CreateAsync(user);
                }

                // Dev convenience: the local dev user is always an admin so
                // /admin is reachable without configuring Admin:Emails.
                if (!await roleManager.RoleExistsAsync("Admin"))
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                if (!await userManager.IsInRoleAsync(user, "Admin"))
                    await userManager.AddToRoleAsync(user, "Admin");

                await signInManager.SignInAsync(user, isPersistent: true);
                return Results.Redirect("/");
            }).AllowAnonymous();
        }

        return app;
    }

    /// <summary>Re-syncs display name / avatar from the provider on every
    /// login — profile edits made upstream show up automatically.</summary>
    internal static async Task SyncExternalProfileAsync(
        UserManager<AppUser> userManager, ExternalLoginInfo info, AppUser user)
    {
        var name = info.Principal.FindFirstValue(ClaimTypes.Name);
        var picture = info.Principal.FindFirstValue("picture");

        var changed = false;
        if (!string.IsNullOrWhiteSpace(name) && user.DisplayName != name)
        {
            user.DisplayName = name;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(picture) && user.AvatarUrl != picture)
        {
            user.AvatarUrl = picture;
            changed = true;
        }

        if (changed)
            await userManager.UpdateAsync(user);
    }

    /// <summary>True when the email is listed in Admin:Emails (case-insensitive).</summary>
    internal static bool IsAdminEmail(IConfiguration config, string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        return config.GetSection("Admin:Emails").GetChildren()
            .Select(v => v.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Contains(email, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Members of Admin:Emails land in the Admin role (created on
    /// first use) — idempotent and safe to call on every login.</summary>
    internal static async Task EnsureAdminAsync(
        UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager,
        IConfiguration config, AppUser user)
    {
        if (!IsAdminEmail(config, user.Email)) return;

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await userManager.IsInRoleAsync(user, "Admin"))
            await userManager.AddToRoleAsync(user, "Admin");
    }

    // Rejects absolute URLs, protocol-relative "//evil.com", and "/\evil.com"
    internal static string SafeReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl)
           && returnUrl[0] == '/'
           && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            ? returnUrl
            : "/";
}