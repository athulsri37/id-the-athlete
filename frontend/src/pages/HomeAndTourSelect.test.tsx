import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import HomeScreen from "./HomeScreen";
import TourSelect from "./TourSelect";
import { groupByFamily } from "../config/sportFamilies";
import { Sport } from "../types";

const SPORTS: Sport[] = [
  { slug: "cricket-men-international", name: "Men's International Cricket", shortName: "Men's International", family: "cricket", maxGuesses: 8, hintAfterGuess: 5 },
  { slug: "tennis-men", name: "Men's Tennis", shortName: "Men's", family: "tennis", maxGuesses: 8, hintAfterGuess: 5 },
  { slug: "tennis-women", name: "Women's Tennis", shortName: "Women's", family: "tennis", maxGuesses: 8, hintAfterGuess: 5 },
];

describe("HomeScreen", () => {
  it("shows one button per family from the API data, in order", () => {
    const onSelectFamily = vi.fn();
    render(<HomeScreen families={groupByFamily(SPORTS)} loadFailed={false} onSelectFamily={onSelectFamily} />);

    const buttons = screen.getAllByRole("button").map((b) => b.textContent);
    expect(buttons).toEqual(["Tennis", "Cricket"]);

    fireEvent.click(screen.getByRole("button", { name: "Cricket" }));
    expect(onSelectFamily).toHaveBeenCalledWith("cricket");
  });

  it("says so when the sports couldn't be loaded", () => {
    render(<HomeScreen families={null} loadFailed={true} onSelectFamily={() => {}} />);

    expect(screen.getByText(/Couldn't load the sports/)).toBeInTheDocument();
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});

describe("TourSelect", () => {
  it("shows the family's title and its tours' short labels", () => {
    const onSelectTour = vi.fn();
    const tennis = groupByFamily(SPORTS)[0];
    render(<TourSelect group={tennis} onSelectTour={onSelectTour} onBack={() => {}} />);

    expect(screen.getByRole("heading")).toHaveTextContent("ID the Tennis Player");
    fireEvent.click(screen.getByRole("button", { name: "Women's" }));
    expect(onSelectTour).toHaveBeenCalledWith(SPORTS[2]);
  });

  it("works for a new sport with no display entry or short names", () => {
    const football = groupByFamily([{ slug: "football-epl", name: "Premier League", shortName: "", family: "football", maxGuesses: 8, hintAfterGuess: 5 }])[0];
    render(<TourSelect group={football} onSelectTour={() => {}} onBack={() => {}} />);

    expect(screen.getByRole("heading")).toHaveTextContent("ID the Football Player");
    expect(screen.getByRole("button", { name: "Premier League" })).toBeInTheDocument();
  });
});
