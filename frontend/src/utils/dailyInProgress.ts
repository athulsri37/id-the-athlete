// The Daily Challenge game in progress, saved after every guess so a
// refresh resumes it instead of starting over at guess 1. The last guess
// carries the server's signed game token, so a resumed game continues the
// same server-side game: the 8-guess limit holds and the stats record one
// game, not an abandoned one plus a new one.
//
// One per sport, under `dailyInProgress:${sportSlug}`: starting a game on
// another date replaces it, so storage never builds up. A finished game
// moves to dailyCompletion instead (see GameBoard).
//
// Storage can be unavailable (private browsing, blocked site data); then
// nothing is saved and a refresh starts over, as before.
import { GuessResponse } from "../types";

export interface DailyInProgress {
  date: string; // YYYY-MM-DD, the puzzle being played
  guesses: GuessResponse[];
  revealedCountry: string | null;
  hintOffered: boolean;
  hintDeclined: boolean;
}

function storageKey(sportSlug: string): string {
  return `dailyInProgress:${sportSlug}`;
}

// The saved game for this sport and date, or null if there isn't one (or
// the saved game is for another date, or can't be read).
export function getInProgress(sportSlug: string, date: string): DailyInProgress | null {
  try {
    const raw = localStorage.getItem(storageKey(sportSlug));
    if (!raw) return null;
    const saved = JSON.parse(raw) as DailyInProgress;
    return saved.date === date && Array.isArray(saved.guesses) && saved.guesses.length > 0 ? saved : null;
  } catch {
    return null;
  }
}

export function saveInProgress(sportSlug: string, progress: DailyInProgress): void {
  try {
    localStorage.setItem(storageKey(sportSlug), JSON.stringify(progress));
  } catch {
    // Not saved: a refresh starts over, as it did before this existed.
  }
}

export function clearInProgress(sportSlug: string): void {
  try {
    localStorage.removeItem(storageKey(sportSlug));
  } catch {
    // Nothing to clear if storage is unavailable.
  }
}
