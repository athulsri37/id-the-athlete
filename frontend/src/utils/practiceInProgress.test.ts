import { afterEach, describe, expect, it, vi } from "vitest";
import { clearPracticeInProgress, getPracticeInProgress, PracticeInProgress, savePracticeInProgress } from "./practiceInProgress";

const SPORT = "tennis-men";

const progress: PracticeInProgress = {
  sessionId: "session-1",
  guesses: [],
  revealedCountry: null,
  hintOffered: false,
  hintDeclined: false,
};

afterEach(() => {
  sessionStorage.clear();
  vi.restoreAllMocks();
});

describe("practiceInProgress", () => {
  it("keeps the game for its tour and difficulty", () => {
    savePracticeInProgress(SPORT, "easy", progress);

    expect(getPracticeInProgress(SPORT, "easy")).toEqual(progress);
  });

  it("keeps each difficulty and tour separately", () => {
    savePracticeInProgress(SPORT, "easy", progress);

    expect(getPracticeInProgress(SPORT, "hard")).toBeNull();
    expect(getPracticeInProgress("tennis-women", "easy")).toBeNull();
  });

  it("lives in this tab only, not across tabs", () => {
    savePracticeInProgress(SPORT, "easy", progress);

    expect(sessionStorage.getItem("practiceInProgress:tennis-men:easy")).not.toBeNull();
    expect(localStorage.getItem("practiceInProgress:tennis-men:easy")).toBeNull();
  });

  it("forgets a cleared game", () => {
    savePracticeInProgress(SPORT, "easy", progress);
    clearPracticeInProgress(SPORT, "easy");

    expect(getPracticeInProgress(SPORT, "easy")).toBeNull();
  });

  it("ignores something unreadable", () => {
    sessionStorage.setItem("practiceInProgress:tennis-men:easy", "{not json");
    expect(getPracticeInProgress(SPORT, "easy")).toBeNull();

    sessionStorage.setItem("practiceInProgress:tennis-men:easy", JSON.stringify({ guesses: [] }));
    expect(getPracticeInProgress(SPORT, "easy")).toBeNull();
  });

  it("carries on without storage, keeping nothing", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });

    expect(() => savePracticeInProgress(SPORT, "easy", progress)).not.toThrow();
    expect(getPracticeInProgress(SPORT, "easy")).toBeNull();
  });
});
