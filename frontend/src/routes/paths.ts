// The game's web addresses, built and read in one place:
//   /                          home: pick a sport
//   /tennis                    pick a tour
//   /tennis/men                a tour's home: pick a mode
//   /tennis/men/daily          today's Daily Challenge
//   /tennis/men/easy           practice (easy, medium or hard)
//   /tennis/men/past           Past Challenges
//   /tennis/men/past/2026-10-12  one Past Challenge
//   /how-to-play               the rules (#tennis, #cricket: a sport's section)
// The sport part is the tour's family and the tour part its urlName (or its
// slug when it has none), both from GET /api/sports, so a new tour gets its
// addresses with no change here.
import { Difficulty, Sport } from "../types";

export const MODES: readonly Difficulty[] = ["daily", "easy", "medium", "hard"];

export function isMode(value: string): value is Difficulty {
  return (MODES as readonly string[]).includes(value);
}

function tourPart(sport: Sport): string {
  return sport.urlName || sport.slug;
}

export const paths = {
  home: () => "/",
  howToPlay: (family?: string) => (family ? `/how-to-play#${family}` : "/how-to-play"),
  family: (family: string) => `/${family}`,
  tour: (sport: Sport) => `/${sport.family}/${tourPart(sport)}`,
  game: (sport: Sport, mode: Difficulty) => `${paths.tour(sport)}/${mode}`,
  past: (sport: Sport) => `${paths.tour(sport)}/past`,
  pastGame: (sport: Sport, date: string) => `${paths.past(sport)}/${date}`,
};

// The tour an address names, if any.
export function findTour(sports: Sport[], family: string, tour: string): Sport | undefined {
  return sports.find((s) => s.family === family && tourPart(s) === tour);
}

// A real calendar date written yyyy-MM-dd (so 2026-02-30 isn't one).
export function isPuzzleDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}
