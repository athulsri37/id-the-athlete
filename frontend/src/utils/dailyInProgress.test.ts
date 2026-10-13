import { afterEach, describe, expect, it, vi } from "vitest";
import { clearInProgress, DailyInProgress, getInProgress, saveInProgress } from "./dailyInProgress";
import { GuessResponse } from "../types";

const SPORT = "tennis-men";
const DATE = "2026-10-13";

const guess: GuessResponse = {
  guessedPlayerName: "Wrong Guess",
  isCorrect: false,
  clues: [],
  gameOver: false,
  answerName: null,
  triviaToken: null,
  gameToken: "signed-token",
  puzzleDate: DATE,
};

const progress: DailyInProgress = {
  date: DATE,
  guesses: [guess, guess],
  revealedCountry: null,
  hintOffered: false,
  hintDeclined: false,
};

afterEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
});

describe("dailyInProgress", () => {
  it("returns the saved game for its sport and date", () => {
    saveInProgress(SPORT, progress);

    expect(getInProgress(SPORT, DATE)).toEqual(progress);
  });

  it("returns nothing for another date or another sport", () => {
    saveInProgress(SPORT, progress);

    expect(getInProgress(SPORT, "2026-10-12")).toBeNull();
    expect(getInProgress("tennis-women", DATE)).toBeNull();
  });

  it("keeps one game per sport: a new date replaces the old one", () => {
    saveInProgress(SPORT, progress);
    saveInProgress(SPORT, { ...progress, date: "2026-10-10" });

    expect(getInProgress(SPORT, DATE)).toBeNull();
    expect(getInProgress(SPORT, "2026-10-10")?.date).toBe("2026-10-10");
  });

  it("forgets the game when cleared", () => {
    saveInProgress(SPORT, progress);

    clearInProgress(SPORT);

    expect(getInProgress(SPORT, DATE)).toBeNull();
  });

  it("ignores a saved game with no guesses or unreadable data", () => {
    saveInProgress(SPORT, { ...progress, guesses: [] });
    expect(getInProgress(SPORT, DATE)).toBeNull();

    localStorage.setItem(`dailyInProgress:${SPORT}`, "{not json");
    expect(getInProgress(SPORT, DATE)).toBeNull();
  });

  it("carries on without storage, e.g. in private browsing", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceededError");
    });
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });
    vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });

    expect(() => saveInProgress(SPORT, progress)).not.toThrow();
    expect(getInProgress(SPORT, DATE)).toBeNull();
    expect(() => clearInProgress(SPORT)).not.toThrow();
  });
});
