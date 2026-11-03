namespace IdTheAthlete.Api.Models;

public class Player
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;

    // Difficulty is normally computed from stats (see GameService), but a
    // curator can override it for a specific player when raw stats don't
    // reflect real-world recognizability (e.g. a well-known player whose
    // title count alone would compute too hard).
    public string? DifficultyOverride { get; set; }
    public bool IsOverridden { get; set; } = false;

    // Set whenever a curator edits any attribute value for this player via
    // the admin tool (see AdminService.UpdatePlayerAsync). Purely a
    // "when was this player last touched" signal for staleness checks
    // elsewhere (e.g. AiTriviaService).
    public DateTime? LastModifiedAt { get; set; }

    // The player's photo: a file on Wikimedia Commons (its name, e.g.
    // "Federer WM16.jpg", never a copy of the image), with the credit and
    // licence its licence requires us to show, and the point the image is
    // centred on when cropped to a small circle (0-100% across and down).
    // File, credit and licence are null, and the centre 50/50, when the
    // player has no photo. Set from the Control Room or
    // the photo review tool (tools/photo_review.py), see
    // AdminService.SetPlayerPhotoAsync.
    public string? PhotoFile { get; set; }
    public string? PhotoCredit { get; set; }
    public string? PhotoLicense { get; set; }
    public int PhotoFocusX { get; set; } = 50;
    public int PhotoFocusY { get; set; } = 50;

    public ICollection<PlayerAttributeValue> AttributeValues { get; set; } = new List<PlayerAttributeValue>();
}
