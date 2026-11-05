import { describe, expect, it } from "vitest";
import { nameMatches, normalizeForSearch } from "./searchText";

describe("searching for a name", () => {
  it("ignores accents and case", () => {
    expect(nameMatches("Iga Świątek", "swiatek")).toBe(true);
    expect(nameMatches("Borna Ćorić", "coric")).toBe(true);
    expect(nameMatches("Jeļena Ostapenko", "JELENA")).toBe(true);
    expect(nameMatches("Garbiñe Muguruza", "garbine")).toBe(true);
  });

  it("finds accented names when the query is typed with accents too", () => {
    expect(nameMatches("Iga Świątek", "Świą")).toBe(true);
    expect(nameMatches("Feliciano López", "lópez")).toBe(true);
  });

  it("matches letters that aren't plain letters with an accent", () => {
    expect(nameMatches("Łukasz Kubot", "lukasz")).toBe(true);
    expect(normalizeForSearch("Søren")).toBe("soren");
    expect(normalizeForSearch("Đoković")).toBe("dokovic");
    expect(normalizeForSearch("Straße")).toBe("strasse");
  });

  it("ignores spaces around the query, and still needs the letters in order", () => {
    expect(nameMatches("Rafael Nadal", "  nadal ")).toBe(true);
    expect(nameMatches("Rafael Nadal", "ladan")).toBe(false);
  });
});
