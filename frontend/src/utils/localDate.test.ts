import { describe, expect, it } from "vitest";
import { todayUtcDateString } from "./localDate";

// Smoke test proving the Vitest harness is wired up correctly -- not a full
// spec of date logic. Real test coverage is deferred follow-up work.
describe("todayUtcDateString", () => {
  it("returns today's UTC date formatted as YYYY-MM-DD", () => {
    expect(todayUtcDateString()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
