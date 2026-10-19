import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import ClueGrid from "./ClueGrid";
import { AttributeDefinition, GuessResponse } from "../types";

const attrs = (n: number): AttributeDefinition[] =>
  Array.from({ length: n }, (_, i) => ({ key: `a${i}`, label: `Column ${i}` }));

const guess: GuessResponse = {
  guessedPlayerName: "Juan Carlos Ferrero",
  isCorrect: false,
  clues: [
    { attributeKey: "a0", label: "Column 0", type: "categorical", value: "Switzerland", isMatch: false, isClose: false, direction: null },
    { attributeKey: "a1", label: "Column 1", type: "numeric", value: "14", isMatch: false, isClose: true, direction: "up" },
  ],
  gameOver: false,
  answerName: null,
  triviaToken: null,
  gameToken: null,
  puzzleDate: null,
};

describe("ClueGrid", () => {
  it("never gets narrower than its columns need, so it scrolls instead of squashing clues", () => {
    const { container: tennis } = render(<ClueGrid guesses={[]} attributes={attrs(8)} />);
    const { container: cricket } = render(<ClueGrid guesses={[]} attributes={attrs(9)} />);
    const minWidth = (c: HTMLElement) => parseFloat((c.querySelector(".overflow-x-auto > div") as HTMLElement).style.minWidth);

    expect(minWidth(cricket)).toBeGreaterThan(minWidth(tennis));
    expect(minWidth(tennis)).toBeGreaterThan(40); // rem: wider than any phone
  });

  it("pins the guess number and player while the clues scroll sideways", () => {
    render(<ClueGrid guesses={[guess]} attributes={attrs(2)} />);

    const player = screen.getByText("Juan Carlos Ferrero").parentElement!;
    expect(player.className).toMatch(/\bsticky\b/);
    expect(screen.getByText("1").parentElement!.className).toMatch(/\bsticky\b/);
    expect(screen.getByText("Player").className).toMatch(/\bsticky\b/);
  });

  it("keeps each clue's value and icon inside its own cell", () => {
    render(<ClueGrid guesses={[guess]} attributes={attrs(2)} />);

    const cell = screen.getByText("Switzerland").parentElement!;
    expect(cell).toHaveTextContent("Switzerland✕");
    expect(cell.className).toMatch(/flex-wrap/);
    expect(cell.className).toMatch(/clue-miss/);
  });
});
