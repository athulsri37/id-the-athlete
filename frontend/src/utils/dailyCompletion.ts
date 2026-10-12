// Per-sport, per-DATE "already played" tracking for the Daily Challenge,
// persisted in localStorage under `dailyCompletion:${sportSlug}:${date}`.
// Keyed by date (not just sport) so completion state for many different
// days -- today's puzzle and any number of past ones via Past Challenges --
// can coexist without overwriting each other. Storing the full guess list
// lets a completed game be re-displayed read-only (Wordle-style) instead of
// allowing a fresh attempt.
import { GuessResponse } from "../types";

export interface DailyCompletion {
  date: string; // YYYY-MM-DD
  won: boolean;
  guesses: GuessResponse[];
  revealedCountry: string | null;
  // Saved when it arrives, after the result, so a replay shows it without
  // another request. Missing until then, and if trivia was unavailable.
  triviaBlurb?: string | null;
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
    const completion = JSON.parse(raw) as DailyCompletion;
    // Completions saved before trivia had its own request kept the blurb on
    // the final guess instead.
    if (completion.triviaBlurb === undefined) {
      const finalGuess = completion.guesses[completion.guesses.length - 1] as { triviaBlurb?: string | null } | undefined;
      completion.triviaBlurb = finalGuess?.triviaBlurb ?? null;
    }
    return completion;
  } catch {
    return null;
  }
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

// Adds the trivia blurb to an already recorded completion. Does nothing if
// the date has no completion.
export function recordDailyTrivia(sportSlug: string, date: string, triviaBlurb: string): void {
  const completion = getCompletionForDate(sportSlug, date);
  if (!completion) return;
  localStorage.setItem(storageKey(sportSlug, date), JSON.stringify({ ...completion, triviaBlurb }));
}
