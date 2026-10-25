import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import GuessCards from "./GuessCards";
import { ClueResult, GuessResponse } from "../types";

const clue = (key: string, value: string, extra: Partial<ClueResult> = {}): ClueResult => ({
  attributeKey: key,
  label: key[0].toUpperCase() + key.slice(1),
  type: "categorical",
  value,
  isMatch: false,
  isClose: false,
  direction: null,
  ...extra,
});

const guess = (name: string, clues: ClueResult[]): GuessResponse => ({
  guessedPlayerName: name,
  isCorrect: false,
  clues,
  gameOver: false,
  answerName: null,
  triviaToken: null,
  gameToken: null,
  puzzleDate: null,
});

const root = guess("Joe Root", [
  clue("country", "England"),
  clue("matches", "374", { type: "numeric", isClose: true, direction: "down" }),
  clue("role", "Batter", { isMatch: true }),
]);
const kane = guess("Kane Williamson", [
  clue("country", "New Zealand", { isClose: true }),
  clue("matches", "370", { type: "numeric", isClose: true, direction: "down" }),
  clue("role", "Batter", { isMatch: true }),
]);
const warner = guess("David Warner", [
  clue("country", "Australia", { isMatch: true }),
  clue("matches", "383", { type: "numeric", direction: "down" }),
  clue("role", "Batter", { isMatch: true }),
]);

const toggle = (name: string) => screen.queryByRole("button", { name: new RegExp(name) });

describe("GuessCards", () => {
  it("asks for a first guess when there are none", () => {
    render(<GuessCards guesses={[]} />);

    expect(screen.getByText("Make your first guess to see clues appear here.")).toBeInTheDocument();
  });

  it("opens the latest guess with every clue labelled, newest first", () => {
    render(<GuessCards guesses={[root, kane, warner]} />);

    const latest = screen.getByRole("region", { name: "Guess 3: David Warner" });
    expect(within(latest).getByText("Country")).toBeInTheDocument();
    expect(within(latest).getByText("Australia").parentElement).toHaveTextContent("Australia✓");
    expect(within(latest).getByText("383").parentElement).toHaveTextContent("383▼");
    expect(within(latest).getByText("383").parentElement!.className).toMatch(/clue-miss/);

    const rows = screen.getAllByRole("button");
    expect(rows.map((b) => b.getAttribute("aria-label"))).toEqual(["Guess 2: Kane Williamson", "Guess 1: Joe Root"]);
  });

  it("folds earlier guesses to their name and a square per clue, coloured by result", () => {
    render(<GuessCards guesses={[root, kane, warner]} />);

    const row = toggle("Joe Root")!;
    expect(row).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("England")).not.toBeInTheDocument();
    const squares = Array.from(row.querySelectorAll("[data-state]")).map((s) => s.getAttribute("data-state"));
    expect(squares).toEqual(["miss", "close", "match"]);
  });

  it("opens a folded guess on a tap and folds it on another", () => {
    render(<GuessCards guesses={[root, kane, warner]} />);

    fireEvent.click(toggle("Joe Root")!);
    expect(toggle("Joe Root")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("England")).toBeInTheDocument();
    expect(toggle("Kane Williamson")).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(toggle("Joe Root")!);
    expect(toggle("Joe Root")).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("England")).not.toBeInTheDocument();
  });

  it("keeps several opened guesses open together", () => {
    render(<GuessCards guesses={[root, kane, warner]} />);

    fireEvent.click(toggle("Joe Root")!);
    fireEvent.click(toggle("Kane Williamson")!);

    expect(screen.getByText("England")).toBeInTheDocument();
    expect(screen.getByText("New Zealand")).toBeInTheDocument();
  });

  it("folds every earlier guess again once the next guess arrives", () => {
    const { rerender } = render(<GuessCards guesses={[root, kane]} />);
    fireEvent.click(toggle("Joe Root")!);
    expect(screen.getByText("England")).toBeInTheDocument();

    rerender(<GuessCards guesses={[root, kane, warner]} />);

    expect(screen.queryByText("England")).not.toBeInTheDocument();
    expect(toggle("Joe Root")).toHaveAttribute("aria-expanded", "false");
    expect(toggle("Kane Williamson")).toHaveAttribute("aria-expanded", "false");
  });

  it("never folds the latest guess", () => {
    render(<GuessCards guesses={[root]} />);

    expect(screen.getByRole("region", { name: "Guess 1: Joe Root" })).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("lays clues out three to a row when they divide by three, otherwise four", () => {
    const nine = guess("Nine", Array.from({ length: 9 }, (_, i) => clue(`c${i}`, `v${i}`)));
    const eight = guess("Eight", Array.from({ length: 8 }, (_, i) => clue(`c${i}`, `v${i}`)));
    const columns = (g: GuessResponse) => {
      const { container, unmount } = render(<GuessCards guesses={[g]} />);
      const grid = container.querySelector<HTMLElement>("[style*='grid-template-columns']")!;
      const value = grid.style.gridTemplateColumns;
      unmount();
      return value;
    };

    expect(columns(nine)).toBe("repeat(3, minmax(0, 1fr))");
    expect(columns(eight)).toBe("repeat(4, minmax(0, 1fr))");
  });

  it("shows a revealed country as the match it is", () => {
    render(<GuessCards guesses={[warner]} revealedCountry="australia" />);

    expect(screen.getByText("Australia").parentElement!.className).toMatch(/bg-\[var\(--accent\)\]/);
  });
});
