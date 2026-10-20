// The practice game being played, kept for this browser tab so leaving the
// game (How to Play, a refresh, the Back button) and coming back carries on
// with it instead of starting a new random player. It holds the server's
// session ID, so the resumed game is the same server-side game, as long as
// the server still has it (see PracticeSessionService: it forgets a game
// left 90 minutes without a guess, and every game when it restarts).
//
// sessionStorage, not localStorage: practice stays casual, so closing the
// tab ends the game. One per tour and difficulty, under
// `practiceInProgress:${sportSlug}:${mode}`; starting a new one replaces it.
//
// Storage can be unavailable (private browsing, blocked site data); then
// nothing is kept and coming back starts a new game, as before.
import { Difficulty, GuessResponse } from "../types";

export interface PracticeInProgress {
  sessionId: string;
  guesses: GuessResponse[];
  revealedCountry: string | null;
  hintOffered: boolean;
  hintDeclined: boolean;
}

function storageKey(sportSlug: string, mode: Difficulty): string {
  return `practiceInProgress:${sportSlug}:${mode}`;
}

// The kept game for this tour and difficulty, or null if there isn't one
// (or it can't be read).
export function getPracticeInProgress(sportSlug: string, mode: Difficulty): PracticeInProgress | null {
  try {
    const raw = sessionStorage.getItem(storageKey(sportSlug, mode));
    if (!raw) return null;
    const saved = JSON.parse(raw) as PracticeInProgress;
    return typeof saved.sessionId === "string" && saved.sessionId && Array.isArray(saved.guesses) ? saved : null;
  } catch {
    return null;
  }
}

export function savePracticeInProgress(sportSlug: string, mode: Difficulty, progress: PracticeInProgress): void {
  try {
    sessionStorage.setItem(storageKey(sportSlug, mode), JSON.stringify(progress));
  } catch {
    // Not kept: coming back starts a new game, as it did before this existed.
  }
}

export function clearPracticeInProgress(sportSlug: string, mode: Difficulty): void {
  try {
    sessionStorage.removeItem(storageKey(sportSlug, mode));
  } catch {
    // Nothing to clear if storage is unavailable.
  }
}
