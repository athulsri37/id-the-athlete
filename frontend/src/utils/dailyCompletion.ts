// Per-sport, per-day "already played" tracking for the Daily Challenge,
// persisted in localStorage under `dailyCompletion:${sportSlug}` (same
// pattern as dailyStreak). Storing the full guess list lets a completed
// game be re-displayed read-only (Wordle-style) instead of allowing a
// fresh attempt, and naturally stops applying once the stored date is no
// longer today.
import { GuessResponse } from "../types";
import { todayLocalDateString } from "./localDate";

export interface DailyCompletion {
  date: string; // YYYY-MM-DD
  won: boolean;
  guesses: GuessResponse[];
  revealedCountry: string | null;
}

function storageKey(sportSlug: string): string {
  return `dailyCompletion:${sportSlug}`;
}

// Returns today's completed Daily Challenge for a sport, or null if today's
// puzzle hasn't been finished yet (including if the stored record is from
// an earlier day).
export function getTodaysCompletion(sportSlug: string): DailyCompletion | null {
  const raw = localStorage.getItem(storageKey(sportSlug));
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as DailyCompletion;
    return parsed.date === todayLocalDateString() ? parsed : null;
  } catch {
    return null;
  }
}

export function recordDailyCompletion(
  sportSlug: string,
  won: boolean,
  guesses: GuessResponse[],
  revealedCountry: string | null
): void {
  const completion: DailyCompletion = {
    date: todayLocalDateString(),
    won,
    guesses,
    revealedCountry,
  };
  localStorage.setItem(storageKey(sportSlug), JSON.stringify(completion));
}