import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import ShareResult, { buildShareText } from "./ShareResult";
import { ClueResult, GuessResponse, Sport } from "../types";

const TENNIS_MEN: Sport = {
  slug: "tennis-men",
  name: "Men's Tennis",
  shortName: "Men's",
  family: "tennis",
  urlName: "men",
  maxGuesses: 8,
  hintAfterGuess: 5,
};

// One clue per letter: m = match, c = close, x = miss.
function guess(pattern: string, opts: { correct?: boolean; country?: string } = {}): GuessResponse {
  const clues: ClueResult[] = [...pattern].map((p, i) => ({
    attributeKey: i === 0 ? "country" : `col${i}`,
    label: `Column ${i}`,
    type: "categorical",
    value: i === 0 ? opts.country ?? "Spain" : "v",
    isMatch: p === "m",
    isClose: p === "c",
    direction: null,
  }));
  return {
    guessedPlayerName: "Someone",
    isCorrect: opts.correct ?? false,
    clues,
    gameOver: opts.correct ?? false,
    answerName: null,
    triviaToken: null,
    gameToken: null,
    puzzleDate: null,
  };
}

const ORIGIN = "https://id-the-athlete.onrender.com";

describe("buildShareText", () => {
  it("names the game and day, scores it, colours every clue, and links back to it", () => {
    const text = buildShareText({
      guesses: [guess("xmxc"), guess("mmmm", { correct: true })],
      won: true,
      sport: TENNIS_MEN,
      mode: "daily",
      puzzleDate: "2026-10-19",
      isPastDate: false,
      winEmoji: "🎾",
      revealedCountry: null,
      origin: ORIGIN,
    });

    expect(text).toBe(
      [
        "ID the Athlete · Men's Tennis",
        "Daily · 19 Oct 2026 · 🎾 2/8",
        "⬜🟩⬜🟨",
        "🟩🟩🟩🟩",
        "",
        "https://id-the-athlete.onrender.com/tennis/men/daily",
      ].join("\n")
    );
  });

  it("scores a lost game X out of the limit", () => {
    const text = buildShareText({
      guesses: Array.from({ length: 8 }, () => guess("xxxx")),
      won: false,
      sport: TENNIS_MEN,
      mode: "daily",
      puzzleDate: "2026-10-19",
      isPastDate: false,
      winEmoji: "🎾",
      revealedCountry: null,
      origin: ORIGIN,
    });

    expect(text.split("\n")[1]).toBe("Daily · 19 Oct 2026 · X/8");
  });

  it("links a Past Challenge to that day's puzzle, which works any day", () => {
    const text = buildShareText({
      guesses: [guess("mmmm", { correct: true })],
      won: true,
      sport: TENNIS_MEN,
      mode: "daily",
      puzzleDate: "2026-10-12",
      isPastDate: true,
      revealedCountry: null,
      origin: ORIGIN,
    });

    expect(text.split("\n")[1]).toBe("Past Challenge · 12 Oct 2026 · 1/8");
    expect(text.endsWith(`${ORIGIN}/tennis/men/past/2026-10-12`)).toBe(true);
  });

  it("names a practice game by its difficulty and links to a new one", () => {
    const text = buildShareText({
      guesses: [guess("mmmm", { correct: true })],
      won: true,
      sport: TENNIS_MEN,
      mode: "hard",
      isPastDate: false,
      winEmoji: "🎾",
      revealedCountry: null,
      origin: ORIGIN,
    });

    expect(text.split("\n")[1]).toBe("Practice (Hard) · 🎾 1/8");
    expect(text.endsWith(`${ORIGIN}/tennis/men/hard`)).toBe(true);
  });

  it("shows the country the hint revealed as a match, as the grid does", () => {
    const text = buildShareText({
      guesses: [guess("xxxx", { country: "Croatia" }), guess("xxxx", { country: "Spain" })],
      won: false,
      sport: TENNIS_MEN,
      mode: "daily",
      puzzleDate: "2026-10-19",
      isPastDate: false,
      revealedCountry: "croatia",
      origin: ORIGIN,
    });

    expect(text.split("\n").slice(2, 4)).toEqual(["🟩⬜⬜⬜", "⬜⬜⬜⬜"]);
  });
});

describe("ShareResult", () => {
  const props = {
    guesses: [guess("mmmm", { correct: true })],
    won: true,
    sport: TENNIS_MEN,
    mode: "daily" as const,
    puzzleDate: "2026-10-19",
    isPastDate: false,
    winEmoji: "🎾",
    revealedCountry: null,
  };

  afterEach(() => {
    vi.unstubAllGlobals();
    // jsdom has no share sheet; tests that add one remove it again.
    delete (navigator as { share?: unknown }).share;
  });

  function stubClipboard(writeText: (text: string) => Promise<void>) {
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
  }

  it("opens the share sheet with the text where the device has one", async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "share", { value: share, configurable: true });
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);
    render(<ShareResult {...props} />);

    fireEvent.click(screen.getByRole("button", { name: "Share result" }));

    await vi.waitFor(() => expect(share).toHaveBeenCalled());
    expect(share.mock.calls[0][0].text).toContain("Daily · 19 Oct 2026 · 🎾 1/8");
    expect(writeText).not.toHaveBeenCalled();
  });

  it("copies nothing when the share sheet is closed", async () => {
    const share = vi.fn().mockRejectedValue(new DOMException("closed", "AbortError"));
    Object.defineProperty(navigator, "share", { value: share, configurable: true });
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);
    render(<ShareResult {...props} />);

    fireEvent.click(screen.getByRole("button", { name: "Share result" }));

    await vi.waitFor(() => expect(share).toHaveBeenCalled());
    expect(writeText).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Share result" })).toBeInTheDocument();
  });

  it("copies the text where there's no share sheet, and says so", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);
    render(<ShareResult {...props} />);

    fireEvent.click(screen.getByRole("button", { name: "Share result" }));

    expect(await screen.findByRole("button", { name: "Copied to clipboard!" })).toBeInTheDocument();
    expect(writeText.mock.calls[0][0]).toContain("/tennis/men/daily");
  });

  it("shows the text to copy by hand when copying isn't allowed", async () => {
    stubClipboard(() => Promise.reject(new Error("not allowed")));
    render(<ShareResult {...props} />);

    fireEvent.click(screen.getByRole("button", { name: "Share result" }));

    const box = await screen.findByLabelText(/Copy this instead/);
    expect((box as HTMLTextAreaElement).value).toContain("ID the Athlete · Men's Tennis");
  });
});
