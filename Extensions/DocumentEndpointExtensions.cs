using System.Security.Claims;
using chatbotchaytau.Services.Documents;

namespace chatbotchaytau.Extensions;

public static class DocumentEndpointExtensions
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/documents/{name}", (string name, ClaimsPrincipal user, IWebHostEnvironment env) =>
        {
            // Files live under App_Data/documents/{ownerId}/{name}; resolve
            // strictly inside the caller's own directory (see DocumentPaths).
            var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var path = DocumentPaths.Resolve(env.ContentRootPath, ownerId, name);
            if (path is null || !File.Exists(path)) return Results.NotFound();

            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".pdf"  => "application/pdf",
                ".md"   => "text/markdown; charset=utf-8",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _       => "application/octet-stream",
            };
            return Results.File(path, type, enableRangeProcessing: true);
        }).RequireAuthorization();

        return app;
    }
}
