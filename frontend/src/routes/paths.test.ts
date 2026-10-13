import { describe, expect, it } from "vitest";
import { findTour, isMode, isPuzzleDate, paths } from "./paths";
import { Sport } from "../types";

const sport = (slug: string, family: string, urlName: string): Sport => ({
  slug,
  name: slug,
  shortName: "",
  family,
  urlName,
  maxGuesses: 8,
  hintAfterGuess: 5,
});

const MEN_CRICKET = sport("cricket-men-international", "cricket", "men");
const WOMEN_TENNIS = sport("tennis-women", "tennis", "women");
const NO_URL_NAME = sport("football-epl", "football", "");
const SPORTS = [MEN_CRICKET, WOMEN_TENNIS, NO_URL_NAME];

describe("paths", () => {
  it("builds every screen's address from the tour's family and urlName", () => {
    expect(paths.home()).toBe("/");
    expect(paths.family("cricket")).toBe("/cricket");
    expect(paths.tour(MEN_CRICKET)).toBe("/cricket/men");
    expect(paths.game(MEN_CRICKET, "daily")).toBe("/cricket/men/daily");
    expect(paths.game(WOMEN_TENNIS, "hard")).toBe("/tennis/women/hard");
    expect(paths.past(WOMEN_TENNIS)).toBe("/tennis/women/past");
    expect(paths.pastGame(WOMEN_TENNIS, "2026-10-12")).toBe("/tennis/women/past/2026-10-12");
  });

  it("falls back to the slug for a tour with no urlName", () => {
    expect(paths.tour(NO_URL_NAME)).toBe("/football/football-epl");
  });
});

describe("findTour", () => {
  it("finds the tour an address names, and nothing for any other", () => {
    expect(findTour(SPORTS, "cricket", "men")).toBe(MEN_CRICKET);
    expect(findTour(SPORTS, "football", "football-epl")).toBe(NO_URL_NAME);
    expect(findTour(SPORTS, "tennis", "men")).toBeUndefined();
    expect(findTour(SPORTS, "cricket", "cricket-men-international")).toBeUndefined();
  });
});

describe("isMode", () => {
  it("accepts the four modes only", () => {
    expect(["daily", "easy", "medium", "hard"].every(isMode)).toBe(true);
    expect(isMode("past")).toBe(false);
    expect(isMode("Daily")).toBe(false);
  });
});

describe("isPuzzleDate", () => {
  it("accepts real yyyy-MM-dd dates only", () => {
    expect(isPuzzleDate("2026-10-12")).toBe(true);
    expect(isPuzzleDate("2024-02-29")).toBe(true);
    expect(isPuzzleDate("2026-02-30")).toBe(false);
    expect(isPuzzleDate("2026-13-01")).toBe(false);
    expect(isPuzzleDate("12-10-2026")).toBe(false);
    expect(isPuzzleDate("2026-10-12x")).toBe(false);
  });
});
