using Microsoft.AspNetCore.Identity;

namespace chatbotchaytau.Data;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}