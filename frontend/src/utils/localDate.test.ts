import { describe, expect, it } from "vitest";
import { addDays, todayUtcDateString } from "./localDate";

// Smoke test proving the Vitest harness is wired up correctly -- not a full
// spec of date logic. Real test coverage is deferred follow-up work.
describe("todayUtcDateString", () => {
  it("returns today's UTC date formatted as YYYY-MM-DD", () => {
    expect(todayUtcDateString()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});

describe("addDays", () => {
  it("moves a date by whole days across month and year ends", () => {
    expect(addDays("2026-10-13", -29)).toBe("2026-09-14");
    expect(addDays("2026-12-31", 1)).toBe("2027-01-01");
    expect(addDays("2024-03-01", -1)).toBe("2024-02-29");
  });

  it("ignores daylight-saving changes", () => {
    expect(addDays("2026-03-28", 2)).toBe("2026-03-30");
    expect(addDays("2026-11-02", -1)).toBe("2026-11-01");
  });
});
