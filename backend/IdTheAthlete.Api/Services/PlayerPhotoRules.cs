using System.Text.RegularExpressions;

namespace IdTheAthlete.Api.Services;

// What a player's photo may hold (see Player.PhotoFile), shared by the
// database column sizes (GameDbContext) and the admin validation
// (AdminService.SetPlayerPhotoAsync).
public static partial class PlayerPhotoRules
{
    public const int MaxFileLength = 240; // Commons' own limit on file names
    public const int MaxCreditLength = 300;
    public const int MaxLicenseLength = 100;

    // Image types Commons serves resized thumbnails of.
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".tif", ".tiff", ".svg"];

    // Characters Commons never allows in a file name.
    [GeneratedRegex(@"[#<>\[\]|{}/\\]")]
    private static partial Regex ForbiddenCharacters();

    // A Commons file name as typed or pasted: a "File:" prefix is dropped and
    // underscores (as in a Commons address) become spaces, the form Commons
    // itself shows. Null when it can't be a Commons image file name.
    public static string? NormalizeFile(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return null;

        var name = file.Trim();
        if (name.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            name = name["File:".Length..].Trim();
        name = name.Replace('_', ' ');

        if (name.Length == 0 || name.Length > MaxFileLength || ForbiddenCharacters().IsMatch(name))
            return null;
        if (!Extensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return null;
        return name;
    }

    public static bool IsValidFocus(int percent) => percent is >= 0 and <= 100;
}
