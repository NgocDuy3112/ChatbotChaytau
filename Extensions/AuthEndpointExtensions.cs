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
            string? returnUrl, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager) =>
        {
            var target = SafeReturnUrl(returnUrl);

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/login?error=external");

            var signIn = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (signIn.Succeeded) return Results.Redirect(target);

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
                SignInManager<AppUser> signInManager, UserManager<AppUser> userManager) =>
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
                await signInManager.SignInAsync(user, isPersistent: true);
                return Results.Redirect("/");
            }).AllowAnonymous();
        }

        return app;
    }

    // Rejects absolute URLs, protocol-relative "//evil.com", and "/\evil.com"
    private static string SafeReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl)
           && returnUrl[0] == '/'
           && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            ? returnUrl
            : "/";
}