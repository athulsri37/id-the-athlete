// Per-sport, per-DATE "already played" tracking for the Daily Challenge,
// persisted in localStorage under `dailyCompletion:${sportSlug}:${date}`.
// Keyed by date (not just sport) so completion state for many different
// days -- today's puzzle and any number of past ones via Past Challenges --
// can coexist without overwriting each other. Storing the full guess list
// lets a completed game be re-displayed read-only (Wordle-style) instead of
// allowing a fresh attempt.
import { GuessResponse } from "../types";
import { todayLocalDateString } from "./localDate";

export interface DailyCompletion {
  date: string; // YYYY-MM-DD
  won: boolean;
  guesses: GuessResponse[];
  revealedCountry: string | null;
}

function storageKey(sportSlug: string, date: string): string {
  return `dailyCompletion:${sportSlug}:${date}`;
}

// Returns the completed Daily Challenge for a sport on a specific date, or
// null if that date hasn't been finished yet (or was never played).
export function getCompletionForDate(sportSlug: string, date: string): DailyCompletion | null {
  const raw = localStorage.getItem(storageKey(sportSlug, date));
  if (!raw) return null;
  try {
    return JSON.parse(raw) as DailyCompletion;
  } catch {
    return null;
  }
}

// Convenience wrapper: today is just "the date = today" special case.
export function getTodaysCompletion(sportSlug: string): DailyCompletion | null {
  return getCompletionForDate(sportSlug, todayLocalDateString());
}

export function recordDailyCompletion(
  sportSlug: string,
  date: string,
  won: boolean,
  guesses: GuessResponse[],
  revealedCountry: string | null
): void {
  const completion: DailyCompletion = { date, won, guesses, revealedCountry };
  localStorage.setItem(storageKey(sportSlug, date), JSON.stringify(completion));
}