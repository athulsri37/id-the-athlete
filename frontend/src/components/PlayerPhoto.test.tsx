import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import PlayerPhoto from "./PlayerPhoto";
import AnswerPhoto from "./AnswerPhoto";

const photo = { file: "Federer WM16 (37).jpg", credit: "si.robi", license: "CC BY-SA 2.0", focusX: 40, focusY: 20 };

describe("a player's photo", () => {
  it("is the Commons thumbnail, centred where it was set, with its credit on hover", () => {
    const { container } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={32} />);

    const img = container.querySelector("img")!;
    expect(img.getAttribute("src")).toBe(
      "https://commons.wikimedia.org/wiki/Special:FilePath/Federer_WM16_(37).jpg?width=120"
    );
    expect(img.style.objectPosition).toBe("40% 20%");
    expect(img.title).toBe("Photo: si.robi, CC BY-SA 2.0");
  });

  it("asks for a larger thumbnail when shown large", () => {
    const { container } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={112} />);
    expect(container.querySelector("img")!.getAttribute("src")).toMatch(/\?width=330$/);
  });

  it("is the player's initials when there's no photo", () => {
    render(<PlayerPhoto name="John Smith (New Zealand)" photo={null} size={32} />);
    expect(screen.getByTestId("player-initials").textContent).toBe("JS");
  });

  it("falls back to the initials when the photo doesn't load", () => {
    const { container } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={32} />);

    fireEvent.error(container.querySelector("img")!);

    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByTestId("player-initials").textContent).toBe("RF");
  });
});

describe("the mystery player's photo on the result screen", () => {
  it("credits the photo, linking to its file on Commons", () => {
    render(<AnswerPhoto name="Roger Federer" photo={photo} />);

    const link = screen.getByRole("link", { name: "Photo: si.robi, CC BY-SA 2.0" });
    expect(link).toHaveAttribute("href", "https://commons.wikimedia.org/wiki/File:Federer_WM16_(37).jpg");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("shows the initials and no credit when there's no photo", () => {
    render(<AnswerPhoto name="Roger Federer" photo={undefined} />);

    expect(screen.getByTestId("player-initials").textContent).toBe("RF");
    expect(screen.queryByRole("link")).toBeNull();
  });
});
