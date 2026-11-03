import { describe, expect, it } from "vitest";
import { playerInitials } from "./initials";

describe("a player's initials", () => {
  it("are the first letters of the first and last names", () => {
    expect(playerInitials("Roger Federer")).toBe("RF");
    expect(playerInitials("Juan Martín del Potro")).toBe("JP");
    expect(playerInitials("Iga Świątek")).toBe("IŚ");
  });

  it("leave out a disambiguation in brackets", () => {
    expect(playerInitials("John Smith (New Zealand)")).toBe("JS");
    expect(playerInitials("John Smith (b. 1985)")).toBe("JS");
  });

  it("skip punctuation, and work with one name", () => {
    expect(playerInitials("M. S. Dhoni")).toBe("MD");
    expect(playerInitials("Mohammad 'Ali")).toBe("MA");
    expect(playerInitials("Kapil")).toBe("K");
    expect(playerInitials("  ")).toBe("?");
  });
});
