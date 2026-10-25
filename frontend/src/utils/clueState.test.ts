import { describe, expect, it } from "vitest";
import { clueState } from "./clueState";
import { ClueResult } from "../types";

const clue = (extra: Partial<ClueResult>): ClueResult => ({
  attributeKey: "country",
  label: "Country",
  type: "categorical",
  value: "Spain",
  isMatch: false,
  isClose: false,
  direction: null,
  ...extra,
});

describe("clueState", () => {
  it("is a match, close or a miss as the server judged it", () => {
    expect(clueState(clue({ isMatch: true }))).toBe("match");
    expect(clueState(clue({ isClose: true }))).toBe("close");
    expect(clueState(clue({}))).toBe("miss");
  });

  it("judges the country by the revealed hint once there is one, ignoring case", () => {
    expect(clueState(clue({ value: "Spain" }), "spain")).toBe("match");
    expect(clueState(clue({ value: "France", isMatch: true }), "Spain")).toBe("miss");
    expect(clueState(clue({ value: "France", isClose: true }), "Spain")).toBe("close");
  });

  it("leaves other clues to the server even with a revealed country", () => {
    expect(clueState(clue({ attributeKey: "plays", value: "Spain" }), "Spain")).toBe("miss");
  });
});
