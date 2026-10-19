import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import App from "./App";
import * as client from "./api/client";
import { Sport } from "./types";

vi.mock("./api/client");

const SPORTS: Sport[] = [
  { slug: "cricket-men-international", name: "Men's International Cricket", shortName: "Men's International", family: "cricket", urlName: "men", maxGuesses: 8, hintAfterGuess: 5 },
  { slug: "tennis-men", name: "Men's Tennis", shortName: "Men's", family: "tennis", urlName: "men", maxGuesses: 8, hintAfterGuess: 5 },
  { slug: "tennis-women", name: "Women's Tennis", shortName: "Women's", family: "tennis", urlName: "women", maxGuesses: 8, hintAfterGuess: 5 },
];

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.mocked(client.fetchActiveTheme).mockResolvedValue("retro");
  vi.mocked(client.fetchSports).mockResolvedValue(SPORTS);
  vi.mocked(client.fetchDailyPuzzleDates).mockResolvedValue(["2026-10-12", "2026-10-11"]);
});

describe("routes", () => {
  it("/ shows the sports", async () => {
    renderAt("/");
    expect(await screen.findByRole("button", { name: "Tennis" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cricket" })).toBeInTheDocument();
  });

  it("/tennis shows Tennis's tours", async () => {
    renderAt("/tennis");
    expect(await screen.findByRole("button", { name: "Men's" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Women's" })).toBeInTheDocument();
  });

  it("/cricket/men shows that tour's modes", async () => {
    renderAt("/cricket/men");
    expect(await screen.findByRole("button", { name: "Daily Challenge" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Past Challenges" })).toBeInTheDocument();
  });

  it("/how-to-play shows the rules", async () => {
    renderAt("/how-to-play");
    expect(await screen.findByRole("heading", { name: "How to play" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Tennis clues" })).toBeInTheDocument();
  });

  it.each([
    "/football",                       // unknown sport
    "/tennis/mixed",                   // unknown tour
    "/cricket/men-international",      // a slug, not the tour's address
    "/tennis/men/practice",            // unknown mode
    "/tennis/men/past/2026-02-30",     // not a real date
    "/tennis/men/past/2020-01-01",     // no puzzle that day
    "/tennis/men/daily/extra",         // nothing this deep
  ])("%s is not found", async (path) => {
    renderAt(path);
    expect(await screen.findByText("Page not found")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go to the home page" })).toHaveAttribute("href", "/");
  });

  it("a deep link says so when the sports can't load", async () => {
    vi.mocked(client.fetchSports).mockRejectedValue(new Error("offline"));
    renderAt("/tennis/men");
    expect(await screen.findByText(/Couldn't load the sports/)).toBeInTheDocument();
  });
});
