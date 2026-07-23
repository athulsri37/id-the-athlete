// Per-sport Daily Challenge streak tracking, persisted in localStorage under
// `dailyStreak:${sportSlug}` so each sport's streak is fully independent.
import { todayLocalDateString } from "./localDate";

export interface StreakData {
  lastWonDate: string; // YYYY-MM-DD, local date of the most recent win
  currentStreak: number;
  maxStreak: number;
}

function storageKey(sportSlug: string): string {
  return `dailyStreak:${sportSlug}`;
}

function isExactlyOneDayBefore(earlier: string, later: string): boolean {
  const earlierDate = new Date(`${earlier}T00:00:00`);
  const laterDate = new Date(`${later}T00:00:00`);
  const diffDays = Math.round((laterDate.getTime() - earlierDate.getTime()) / 86_400_000);
  return diffDays === 1;
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

// Records the outcome of today's Daily Challenge for a sport and returns the
// updated streak. Safe to call multiple times for the same win (won't
// double-count if lastWonDate is already today).
export function recordDailyResult(sportSlug: string, won: boolean): StreakData {
  const today = todayLocalDateString();
  const existing = getStreak(sportSlug);

  if (!won) {
    const next: StreakData = {
      lastWonDate: existing?.lastWonDate ?? "",
      currentStreak: 0,
      maxStreak: existing?.maxStreak ?? 0,
    };
    localStorage.setItem(storageKey(sportSlug), JSON.stringify(next));
    return next;
  }

  if (existing?.lastWonDate === today) {
    return existing;
  }

  const continuesStreak = !existing || isExactlyOneDayBefore(existing.lastWonDate, today);
  const currentStreak = continuesStreak ? (existing?.currentStreak ?? 0) + 1 : 1;
  const maxStreak = Math.max(currentStreak, existing?.maxStreak ?? 0);

  const next: StreakData = { lastWonDate: today, currentStreak, maxStreak };
  localStorage.setItem(storageKey(sportSlug), JSON.stringify(next));
  return next;
}