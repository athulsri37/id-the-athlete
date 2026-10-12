import { afterEach, describe, expect, it } from "vitest";
import { getCompletionForDate, recordDailyCompletion, recordDailyTrivia } from "./dailyCompletion";
import { GuessResponse } from "../types";

const SPORT = "tennis-men";
const DATE = "2026-10-12";

const finalGuess: GuessResponse = {
  guessedPlayerName: "Mystery Player",
  isCorrect: true,
  clues: [],
  gameOver: true,
  answerName: "Mystery Player",
  triviaToken: "token",
  gameToken: null,
};

afterEach(() => {
  localStorage.clear();
});

describe("recordDailyTrivia", () => {
  it("adds the blurb to the recorded completion", () => {
    recordDailyCompletion(SPORT, DATE, true, [finalGuess], null);

    recordDailyTrivia(SPORT, DATE, "A fun fact.");

    expect(getCompletionForDate(SPORT, DATE)).toEqual({
      date: DATE,
      won: true,
      guesses: [finalGuess],
      revealedCountry: null,
      triviaBlurb: "A fun fact.",
    });
  });

  it("does nothing when the date has no completion", () => {
    recordDailyTrivia(SPORT, DATE, "A fun fact.");

    expect(getCompletionForDate(SPORT, DATE)).toBeNull();
  });
});

describe("getCompletionForDate", () => {
  it("has no blurb before trivia arrives", () => {
    recordDailyCompletion(SPORT, DATE, true, [finalGuess], null);

    expect(getCompletionForDate(SPORT, DATE)?.triviaBlurb).toBeNull();
  });

  it("reads the blurb from the final guess of a completion saved before trivia had its own request", () => {
    const legacyGuess = { ...finalGuess, triviaToken: undefined, triviaBlurb: "An old fact." };
    localStorage.setItem(
      `dailyCompletion:${SPORT}:${DATE}`,
      JSON.stringify({ date: DATE, won: true, guesses: [legacyGuess], revealedCountry: null })
    );

    expect(getCompletionForDate(SPORT, DATE)?.triviaBlurb).toBe("An old fact.");
  });
});
