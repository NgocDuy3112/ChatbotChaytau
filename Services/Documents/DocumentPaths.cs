namespace aida.Services.Documents;

/// <summary>
/// Resolves document paths strictly inside App_Data/documents/{ownerId}/ —
/// every lookup is owner-scoped, so one user can never address another
/// user's files, and the file name is stripped of any path segments.
/// </summary>
internal static class DocumentPaths
{
    /// <summary>Absolute path for the file, or null when the owner or name is
    /// unsafe/missing. Does not check existence.</summary>
    internal static string? Resolve(string contentRoot, string? ownerId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return null;

        // Owner ids are Identity GUIDs; reject anything that could escape the
        // owner directory (defense in depth beyond the GUID assumption).
        if (ownerId != Path.GetFileName(ownerId)) return null;

        var safe = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(safe)) return null;

        return Path.Combine(contentRoot, "App_Data", "documents", ownerId, safe);
    }
}
