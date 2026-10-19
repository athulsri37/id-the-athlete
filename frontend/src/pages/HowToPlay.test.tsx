import { beforeAll, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import HowToPlay from "./HowToPlay";
import { Sport } from "../types";

const sport = (slug: string, family: string, maxGuesses = 8, hintAfterGuess = 5): Sport => ({
  slug,
  name: slug,
  shortName: "",
  family,
  urlName: slug,
  maxGuesses,
  hintAfterGuess,
});

function renderWith(sports: Sport[], path = "/how-to-play") {
  render(
    <MemoryRouter initialEntries={[path]}>
      <HowToPlay sports={sports} onBack={() => {}} />
    </MemoryRouter>
  );
}

// The practice list item about Easy, Medium and Hard, which is built from
// several pieces of text.
const sportDetails = (family: string) => document.querySelector(`section#${family} > details`)!;

const practiceLevels = () => screen.getByText(/picks only the best-known players/).closest("li")!;

// jsdom has no layout, so no scrollIntoView; real browsers all do.
beforeAll(() => {
  Element.prototype.scrollIntoView = vi.fn();
});

describe("HowToPlay", () => {
  it("takes the guess limit and hint threshold from the server", () => {
    renderWith([sport("tennis-men", "tennis", 6, 4)]);

    expect(screen.getByText(/Name the mystery player in 6 guesses/)).toBeInTheDocument();
    expect(screen.getByText(/After 4 guesses you're offered one free hint/)).toBeInTheDocument();
  });

  it("has a section per sport with its legend and when each clue is close", () => {
    renderWith([sport("tennis-men", "tennis"), sport("cricket-men-international", "cricket")]);

    const tennis = screen.getByRole("heading", { name: "Tennis clues" }).closest("section")!;
    expect(tennis).toHaveAttribute("id", "tennis");
    expect(within(tennis).getByRole("rowheader", { name: "Career-High Rank" })).toBeInTheDocument();
    expect(within(tennis).getByText("Within 5 places")).toBeInTheDocument();

    const cricket = screen.getByRole("heading", { name: "Cricket clues" }).closest("section")!;
    expect(cricket).toHaveAttribute("id", "cricket");
    expect(within(cricket).getByText(/Within 15% of the answer, or 500 runs/)).toBeInTheDocument();
  });

  it("starts with every sport's section closed", () => {
    renderWith([sport("tennis-men", "tennis"), sport("cricket-men-international", "cricket")]);

    expect(sportDetails("tennis")).not.toHaveAttribute("open");
    expect(sportDetails("cricket")).not.toHaveAttribute("open");
  });

  it("opens the sport the address names, and only that one", () => {
    renderWith([sport("tennis-men", "tennis"), sport("cricket-men-international", "cricket")], "/how-to-play#cricket");

    expect(sportDetails("cricket")).toHaveAttribute("open");
    expect(sportDetails("tennis")).not.toHaveAttribute("open");
  });

  // The browser reports a <details> opening or closing a moment later
  // (its toggle event), hence the finds.
  it("opens and closes a sport's section when its heading is clicked", async () => {
    renderWith([sport("tennis-men", "tennis")]);
    const summary = sportDetails("tennis").querySelector("summary")!;

    fireEvent.click(summary);
    expect(await within(summary).findByText("▾ Hide")).toBeInTheDocument();
    expect(sportDetails("tennis")).toHaveAttribute("open");

    fireEvent.click(summary);
    expect(await within(summary).findByText("▸ Show")).toBeInTheDocument();
    expect(sportDetails("tennis")).not.toHaveAttribute("open");
  });

  it("only shows sections for the sports there are", () => {
    renderWith([sport("tennis-men", "tennis")]);

    expect(screen.queryByRole("heading", { name: "Cricket clues" })).not.toBeInTheDocument();
    expect(practiceLevels().textContent).toMatch(/comes from their career: reaching No\. 1, Grand Slams and career titles in Tennis\.$/);
  });

  it("skips the per-sport section for a sport without its own rules", () => {
    renderWith([sport("football-epl", "football")]);

    expect(document.querySelectorAll("section[id]")).toHaveLength(0);
    expect(screen.getByRole("heading", { name: "Reading the clues" })).toBeInTheDocument();
  });

  it("names both sports' difficulty basis when there are two", () => {
    renderWith([sport("tennis-men", "tennis"), sport("cricket-men-international", "cricket")]);

    expect(practiceLevels().textContent).toMatch(
      /reaching No\. 1, Grand Slams and career titles in Tennis and career matches, runs and wickets in Cricket\.$/
    );
  });

  it("gives the daily reset in UTC and the reader's own time", () => {
    renderWith([sport("tennis-men", "tennis")]);

    expect(screen.getByText(/A new puzzle starts at 00:00 UTC \(.+ your time\)\./)).toBeInTheDocument();
  });
});
