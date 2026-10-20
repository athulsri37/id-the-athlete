import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
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

  it("draws the active theme's scenery for the screen's sport", async () => {
    vi.mocked(client.fetchActiveTheme).mockResolvedValue("stadium");
    const { container } = render(
      <MemoryRouter initialEntries={["/cricket/men"]}>
        <App />
      </MemoryRouter>
    );

    await screen.findByRole("button", { name: "Daily Challenge" });
    expect(container.querySelector('[data-scene="stadium-cricket-tour"]')).toBeInTheDocument();
    expect(container.querySelector('[data-scene^="retro"]')).not.toBeInTheDocument();
  });

  it("falls back to the Retro theme and its scenery when the theme can't load", async () => {
    vi.mocked(client.fetchActiveTheme).mockRejectedValue(new Error("offline"));
    const { container } = render(
      <MemoryRouter initialEntries={["/"]}>
        <App />
      </MemoryRouter>
    );

    await screen.findByRole("button", { name: "Tennis" });
    expect(document.documentElement).toHaveAttribute("data-theme", "retro");
    expect(container.querySelector('[data-scene="retro-home"]')).toBeInTheDocument();
  });

  it("keeps a practice game across a trip to How to Play and back", async () => {
    let started = 0;
    vi.mocked(client.startPracticeGame).mockImplementation(
      async () => ({ sessionId: `session-${++started}` }) as Awaited<ReturnType<typeof client.startPracticeGame>>
    );
    vi.mocked(client.fetchPlayerPool).mockResolvedValue([{ id: 7, name: "Thomas Muster" }]);
    vi.mocked(client.fetchAttributeDefinitions).mockResolvedValue([{ key: "country", label: "Country" }]);
    vi.mocked(client.submitGuess).mockResolvedValue({
      guessedPlayerId: 7,
      guessedPlayerName: "Thomas Muster",
      isCorrect: false,
      clues: [{ attributeKey: "country", label: "Country", type: "categorical", value: "Austria", isMatch: true, isClose: false, direction: null }],
      gameOver: false,
      answerName: null,
      triviaToken: null,
      gameToken: null,
      puzzleDate: null,
    });
    // jsdom has no layout, so no scrollIntoView (How to Play scrolls to a sport).
    Element.prototype.scrollIntoView = vi.fn();
    renderAt("/tennis/men");

    fireEvent.click(await screen.findByRole("button", { name: "Easy" }));
    const input = await screen.findByPlaceholderText("Type a player name");
    await vi.waitFor(() => expect(input).not.toBeDisabled());
    fireEvent.change(input, { target: { value: "Must" } });
    fireEvent.click(await screen.findByRole("button", { name: "Thomas Muster" }));
    await screen.findByText("1 / 8 guesses");

    // To the rules from the clue legend, and back.
    fireEvent.click(screen.getByRole("button", { name: "What do the clues mean?" }));
    fireEvent.click(screen.getByRole("link", { name: /How to play/ }));
    fireEvent.click(await screen.findByRole("button", { name: "← Back" }));

    expect(await screen.findByText("1 / 8 guesses")).toBeInTheDocument();
    expect(client.startPracticeGame).toHaveBeenCalledTimes(1);
  });

  it("a deep link says so when the sports can't load", async () => {
    vi.mocked(client.fetchSports).mockRejectedValue(new Error("offline"));
    renderAt("/tennis/men");
    expect(await screen.findByText(/Couldn't load the sports/)).toBeInTheDocument();
  });
});

describe("startup loader", () => {
  it("shows while the API is waking, then gives way to the screen", async () => {
    let answerSports: (sports: Sport[]) => void = () => {};
    vi.mocked(client.fetchSports).mockReturnValue(new Promise((resolve) => { answerSports = resolve; }));
    renderAt("/");

    const loader = await screen.findByRole("status");
    expect(loader).toHaveTextContent("ID the Athlete");
    // The note is always rendered; CSS (.startup-note) keeps it hidden for the first 3 seconds.
    expect(screen.getByText("Warming up the server. The first visit can take up to a minute.")).toHaveClass("startup-note");
    expect(screen.queryByRole("button", { name: "Tennis" })).not.toBeInTheDocument();

    answerSports(SPORTS);
    expect(await screen.findByRole("button", { name: "Tennis" })).toBeInTheDocument();
    expect(screen.queryByText(/Warming up the server/)).not.toBeInTheDocument();
  });

  it("stays up while only the theme has answered", async () => {
    vi.mocked(client.fetchSports).mockReturnValue(new Promise(() => {}));
    renderAt("/tennis/men");

    expect(await screen.findByText(/Warming up the server/)).toBeInTheDocument();
    expect(screen.queryByText("Page not found")).not.toBeInTheDocument();
  });

  it("gives way to the home screen's message when the API can't be reached", async () => {
    vi.mocked(client.fetchActiveTheme).mockRejectedValue(new Error("timeout"));
    vi.mocked(client.fetchSports).mockRejectedValue(new Error("timeout"));
    renderAt("/");

    expect(await screen.findByText(/Couldn't load the sports/)).toBeInTheDocument();
    expect(screen.queryByText(/Warming up the server/)).not.toBeInTheDocument();
  });
});
