import { afterEach, describe, expect, it, vi } from "vitest";
import { getStreak, recordDailyResult, StreakData } from "./dailyStreak";

const SPORT = "tennis-men";

function saveLegacyStreak(streak: Omit<StreakData, "dateBasis">) {
  localStorage.setItem(`dailyStreak:${SPORT}`, JSON.stringify(streak));
}

afterEach(() => {
  localStorage.clear();
  vi.useRealTimers();
});

describe("recordDailyResult", () => {
  it("starts a streak at 1 on the first win", () => {
    expect(recordDailyResult(SPORT, "2026-10-10", true)).toEqual({
      lastWonDate: "2026-10-10",
      currentStreak: 1,
      maxStreak: 1,
      dateBasis: "utc",
    });
  });

  it("extends the streak on consecutive puzzle dates, across a month boundary", () => {
    recordDailyResult(SPORT, "2026-10-30", true);
    recordDailyResult(SPORT, "2026-10-31", true);
    const streak = recordDailyResult(SPORT, "2026-11-01", true);

    expect(streak.currentStreak).toBe(3);
    expect(streak.maxStreak).toBe(3);
  });

  it("restarts at 1 after a missed day but keeps the best streak", () => {
    recordDailyResult(SPORT, "2026-10-10", true);
    recordDailyResult(SPORT, "2026-10-11", true);
    const streak = recordDailyResult(SPORT, "2026-10-13", true);

    expect(streak.currentStreak).toBe(1);
    expect(streak.maxStreak).toBe(2);
  });

  it("does not count the same puzzle date twice", () => {
    recordDailyResult(SPORT, "2026-10-10", true);
    const streak = recordDailyResult(SPORT, "2026-10-10", true);

    expect(streak.currentStreak).toBe(1);
  });

  it("resets to 0 on a loss and keeps the best streak", () => {
    recordDailyResult(SPORT, "2026-10-10", true);
    recordDailyResult(SPORT, "2026-10-11", true);
    const streak = recordDailyResult(SPORT, "2026-10-12", false);

    expect(streak.currentStreak).toBe(0);
    expect(streak.maxStreak).toBe(2);
    expect(getStreak(SPORT)).toEqual(streak);
  });

  it("keeps each sport's streak separate", () => {
    recordDailyResult(SPORT, "2026-10-10", true);
    recordDailyResult("cricket-men-international", "2026-10-11", true);

    expect(recordDailyResult(SPORT, "2026-10-11", true).currentStreak).toBe(2);
  });

  // The original bug: "today" came from the browser's clock. A player in
  // India who won the Oct 11 puzzle just after local midnight (already Oct 12
  // locally) and the Oct 12 puzzle late that evening (still Oct 12 locally)
  // had both wins filed under Oct 12, so the streak stayed at 1. Streaks now
  // depend only on the puzzle date, whatever the clock says.
  it("ignores the browser clock and timezone", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-10-11T19:00:00Z")); // 00:30 Oct 12 in India
    recordDailyResult(SPORT, "2026-10-11", true);
    vi.setSystemTime(new Date("2026-10-12T17:30:00Z")); // 23:00 Oct 12 in India

    expect(recordDailyResult(SPORT, "2026-10-12", true).currentStreak).toBe(2);
  });
});

describe("recordDailyResult with a streak saved before dates were UTC", () => {
  it("continues when the old local date is a day ahead of its puzzle (east of UTC)", () => {
    saveLegacyStreak({ lastWonDate: "2026-10-12", currentStreak: 5, maxStreak: 5 });

    const streak = recordDailyResult(SPORT, "2026-10-12", true);

    expect(streak.currentStreak).toBe(6);
    expect(streak.dateBasis).toBe("utc");
  });

  it("continues when the old local date is a day behind its puzzle (west of UTC)", () => {
    saveLegacyStreak({ lastWonDate: "2026-10-10", currentStreak: 5, maxStreak: 7 });

    const streak = recordDailyResult(SPORT, "2026-10-12", true);

    expect(streak.currentStreak).toBe(6);
    expect(streak.maxStreak).toBe(7);
  });

  it("still restarts after a real gap", () => {
    saveLegacyStreak({ lastWonDate: "2026-10-08", currentStreak: 5, maxStreak: 5 });

    expect(recordDailyResult(SPORT, "2026-10-12", true).currentStreak).toBe(1);
  });

  it("is strict again after the first win under the new rule", () => {
    saveLegacyStreak({ lastWonDate: "2026-10-10", currentStreak: 5, maxStreak: 5 });
    recordDailyResult(SPORT, "2026-10-11", true);

    expect(recordDailyResult(SPORT, "2026-10-13", true).currentStreak).toBe(1);
  });
});
