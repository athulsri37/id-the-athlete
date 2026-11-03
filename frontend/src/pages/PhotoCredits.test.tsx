import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import PhotoCredits from "./PhotoCredits";
import * as client from "../api/client";
import { Sport } from "../types";

vi.mock("../api/client");

const sport = (slug: string, name: string): Sport => ({
  slug, name, shortName: "", family: "tennis", urlName: "", maxGuesses: 8, hintAfterGuess: 5,
});
const SPORTS = [sport("tennis-men", "Men's Tennis"), sport("tennis-women", "Women's Tennis"), sport("cricket-women-international", "Women's International Cricket")];

describe("the Photo credits page", () => {
  it("lists each tour's photos with their credit, linking to the file", async () => {
    vi.mocked(client.fetchPhotoCredits).mockResolvedValue([
      { sportSlug: "tennis-women", playerName: "Iga Świątek", file: "Iga 2023.jpg", credit: "Jane Doe", license: "CC BY 4.0" },
      { sportSlug: "tennis-men", playerName: "Roger Federer", file: "Federer.jpg", credit: "si.robi", license: "CC BY-SA 2.0" },
    ]);
    render(<PhotoCredits sports={SPORTS} onBack={() => {}} />);

    const men = (await screen.findByRole("heading", { name: "Men's Tennis" })).closest("section")!;
    expect(within(men).getByText("Roger Federer")).toBeInTheDocument();
    expect(within(men).getByRole("link", { name: "si.robi, CC BY-SA 2.0" })).toHaveAttribute(
      "href",
      "https://commons.wikimedia.org/wiki/File:Federer.jpg"
    );
    expect(screen.getByRole("heading", { name: "Women's Tennis" })).toBeInTheDocument();
    // A tour without photos has no section.
    expect(screen.queryByRole("heading", { name: "Women's International Cricket" })).toBeNull();
  });

  it("says when there are no photos yet", async () => {
    vi.mocked(client.fetchPhotoCredits).mockResolvedValue([]);
    render(<PhotoCredits sports={SPORTS} onBack={() => {}} />);
    expect(await screen.findByText("No player photos yet.")).toBeInTheDocument();
  });

  it("says when the credits can't load", async () => {
    vi.mocked(client.fetchPhotoCredits).mockRejectedValue(new Error("network"));
    render(<PhotoCredits sports={SPORTS} onBack={() => {}} />);
    expect(await screen.findByText("Couldn't load the photo credits.")).toBeInTheDocument();
  });
});
