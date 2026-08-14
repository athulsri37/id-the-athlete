// Shared local-date helper so every feature keyed off "today" (streaks,
// daily-completion tracking, etc.) agrees on the same definition of today.
export function todayLocalDateString(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

// UTC counterpart of todayLocalDateString -- for anything that needs to
// agree with the backend's notion of "today," which DailyPuzzleGenerationService
// always computes from DateTime.UtcNow. Browser-local "today" can lag or lead
// the backend's UTC "today" by up to a day depending on the visitor's
// timezone, so "which day is this daily puzzle for" logic must use this, not
// todayLocalDateString.
export function todayUtcDateString(): string {
  const now = new Date();
  const year = now.getUTCFullYear();
  const month = String(now.getUTCMonth() + 1).padStart(2, "0");
  const day = String(now.getUTCDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}
