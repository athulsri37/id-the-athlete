// Per-sport Daily Challenge streak tracking, persisted in localStorage under
// `dailyStreak:${sportSlug}` so each sport's streak is fully independent.
//
// A streak counts consecutive *puzzle dates*: the UTC yyyy-MM-dd the backend
// assigns each daily puzzle (see todayUtcDateString). It deliberately never
// reads the browser's clock or timezone, so a player east of UTC who plays
// just after local midnight, or west of UTC in the evening, gets exactly the
// same streak as anyone else.

export interface StreakData {
  lastWonDate: string; // yyyy-MM-dd puzzle date of the most recent win ("" if none)
  currentStreak: number;
  maxStreak: number;
  // "utc" once lastWonDate is a puzzle date. Streaks saved before this
  // existed hold a browser-local date instead -- see continuesStreak.
  dateBasis?: "utc";
}

const MS_PER_DAY = 86_400_000;

function storageKey(sportSlug: string): string {
  return `dailyStreak:${sportSlug}`;
}

// Whole days from one yyyy-MM-dd date to another, computed in UTC so
// daylight-saving changes can't produce fractional days.
function daysBetween(from: string, to: string): number {
  const toUtcMs = (date: string) => {
    const [year, month, day] = date.split("-").map(Number);
    return Date.UTC(year, month - 1, day);
  };
  return Math.round((toUtcMs(to) - toUtcMs(from)) / MS_PER_DAY);
}

// Whether a win on puzzleDate extends the existing streak. Normally only the
// very next puzzle date does. A streak saved before dates were UTC holds a
// browser-local date that can sit a day either side of its puzzle date, so
// for that one transition a gap of 0-2 days also counts: being generous once
// is better than breaking a real streak because the date basis changed.
function continuesStreak(existing: StreakData, puzzleDate: string): boolean {
  if (!existing.lastWonDate) return false;

  const gap = daysBetween(existing.lastWonDate, puzzleDate);
  return existing.dateBasis === "utc" ? gap === 1 : gap >= 0 && gap <= 2;
}

export function getStreak(sportSlug: string): StreakData | null {
  const raw = localStorage.getItem(storageKey(sportSlug));
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StreakData;
  } catch {
    return null;
  }
}

// Records the outcome of the Daily Challenge for puzzleDate (yyyy-MM-dd) and
// returns the updated streak. Safe to call more than once for the same win:
// a repeat for the same puzzle date isn't counted twice.
export function recordDailyResult(sportSlug: string, puzzleDate: string, won: boolean): StreakData {
  const existing = getStreak(sportSlug);

  let next: StreakData;
  if (!won) {
    next = {
      lastWonDate: existing?.lastWonDate ?? "",
      currentStreak: 0,
      maxStreak: existing?.maxStreak ?? 0,
      dateBasis: existing?.dateBasis,
    };
  } else if (existing?.dateBasis === "utc" && existing.lastWonDate === puzzleDate) {
    return existing;
  } else {
    const currentStreak = existing && continuesStreak(existing, puzzleDate) ? existing.currentStreak + 1 : 1;
    next = {
      lastWonDate: puzzleDate,
      currentStreak,
      maxStreak: Math.max(currentStreak, existing?.maxStreak ?? 0),
      dateBasis: "utc",
    };
  }

  localStorage.setItem(storageKey(sportSlug), JSON.stringify(next));
  return next;
}
