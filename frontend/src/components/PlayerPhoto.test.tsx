import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import PlayerPhoto from "./PlayerPhoto";

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

  it("asks for a larger thumbnail when shown larger", () => {
    const { container: table } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={56} />);
    const { container: result } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={112} />);
    expect(table.querySelector("img")!.getAttribute("src")).toMatch(/\?width=250$/);
    expect(result.querySelector("img")!.getAttribute("src")).toMatch(/\?width=330$/);
  });

  it("zooms in on its centre, with a sharper thumbnail to match", () => {
    const { container } = render(<PlayerPhoto name="Roger Federer" photo={{ ...photo, zoom: 250 }} size={56} />);

    const img = container.querySelector("img")!;
    expect(img.style.transform).toBe("scale(2.5)");
    expect(img.style.transformOrigin).toBe("40% 20%");
    expect(img.getAttribute("src")).toMatch(/\?width=500$/);
  });

  it("shows the whole photo when saved before zoom was added", () => {
    const { container } = render(<PlayerPhoto name="Roger Federer" photo={photo} size={56} />);
    expect(container.querySelector("img")!.style.transform).toBe("");
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
