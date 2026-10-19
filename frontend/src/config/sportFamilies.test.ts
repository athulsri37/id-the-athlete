import { describe, expect, it } from "vitest";
import { familyDisplay, groupByFamily, tourLabel } from "./sportFamilies";
import { Sport } from "../types";

const sport = (slug: string, family: string, shortName = ""): Sport => ({ slug, name: `Name of ${slug}`, shortName, family, urlName: "", maxGuesses: 8, hintAfterGuess: 5 });

describe("groupByFamily", () => {
  it("puts Tennis then Cricket first and any other family after, alphabetically", () => {
    const groups = groupByFamily([
      sport("football-epl", "football"),
      sport("cricket-men-international", "cricket"),
      sport("tennis-men", "tennis"),
      sport("basketball-nba", "basketball"),
      sport("tennis-women", "tennis"),
    ]);

    expect(groups.map((g) => g.family)).toEqual(["tennis", "cricket", "basketball", "football"]);
    expect(groups[0].tours.map((t) => t.slug)).toEqual(["tennis-men", "tennis-women"]);
  });
});

describe("familyDisplay", () => {
  it("uses each known family's own wording and win emoji", () => {
    expect(familyDisplay("tennis")).toMatchObject({ label: "Tennis", titleAccent: "Tennis", titleSuffix: " Player", winEmoji: "🎾" });
    expect(familyDisplay("cricket")).toMatchObject({ label: "Cricket", titleAccent: "Cricketer", titleSuffix: "", winEmoji: "🏏" });
  });

  it("gives a family without an entry plain defaults instead of failing", () => {
    const display = familyDisplay("football");

    expect(display).toMatchObject({ label: "Football", titleAccent: "Football", titleSuffix: " Player" });
    expect(display.winEmoji).toBeUndefined();
    expect(display.legendExamples.length).toBeGreaterThan(0);
    expect(display.clueRules).toEqual([]);
  });

  it.each(["tennis", "cricket", "football"])("%s's legend shows a match, a close and a miss, with distinct descriptions", (family) => {
    const examples = familyDisplay(family).legendExamples;

    expect(new Set(examples.map((e) => e.state))).toEqual(new Set(["match", "close", "miss"]));
    expect(new Set(examples.map((e) => e.description)).size).toBe(examples.length);
  });

  it.each(["tennis", "cricket"])("%s explains when each of its clues is close", (family) => {
    const display = familyDisplay(family);

    expect(display.clueRules.length).toBeGreaterThan(0);
    expect(display.difficultyBasis).toBeTruthy();
  });
});

describe("tourLabel", () => {
  it("uses the short name, or the full name when there isn't one", () => {
    expect(tourLabel(sport("tennis-men", "tennis", "Men's"))).toBe("Men's");
    expect(tourLabel(sport("football-epl", "football"))).toBe("Name of football-epl");
  });
});
