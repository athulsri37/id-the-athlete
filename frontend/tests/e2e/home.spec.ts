import { expect, test } from "@playwright/test";

// Smoke test proving the Playwright harness is wired up correctly against
// the real running dev servers -- not a full E2E spec. Real E2E coverage
// of gameplay flows is deferred follow-up work.
test("home page loads and renders the sport picker", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: /id the athlete/i })).toBeVisible();
  await expect(page.getByRole("button", { name: "Tennis" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Cricket" })).toBeVisible();
});
