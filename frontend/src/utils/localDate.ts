// Today's date as the backend sees it: DailyPuzzleGenerationService always
// computes "today" from UTC. A browser-local "today" can lag or lead it by up
// to a day depending on the visitor's timezone, so everything keyed off "which
// day's puzzle is this" (the daily puzzle, completions, streaks) uses this.
// There is deliberately no browser-local counterpart.
export function todayUtcDateString(): string {
  const now = new Date();
  const year = now.getUTCFullYear();
  const month = String(now.getUTCMonth() + 1).padStart(2, "0");
  const day = String(now.getUTCDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

// A yyyy-MM-dd date moved by whole days (negative for earlier), computed in
// UTC so a daylight-saving change can't shift it.
export function addDays(date: string, days: number): string {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day + days)).toISOString().slice(0, 10);
}
