namespace IdTheAthlete.Api.Services;

// A daily puzzle's mystery player together with the date it's for, so a
// caller that asked for "today" learns which day that actually resolved to.
public sealed record ResolvedDailyPuzzle(int PlayerId, DateOnly PuzzleDate);
