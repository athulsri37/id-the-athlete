import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import Scenery from "./Scenery";
import { SceneKind } from "./scenes";
import { ThemeContext } from "../../theme/ThemeContext";

function renderScenery(theme: string, kind: SceneKind, family?: string) {
  return render(
    <ThemeContext.Provider value={theme}>
      <Scenery kind={kind} family={family} />
    </ThemeContext.Provider>
  );
}

const scenes = (container: HTMLElement) =>
  Array.from(container.querySelectorAll("[data-scene]")).map((el) => el.getAttribute("data-scene"));

describe("Scenery", () => {
  it("draws the full scene and the phone one, for CSS to choose between", () => {
    const { container } = renderScenery("retro", "game", "cricket");

    expect(scenes(container)).toEqual(["retro-cricket-page", "retro-cricket-corner"]);
  });

  it("gives a tour's screens the venue on phones where the theme has one", () => {
    const stadium = renderScenery("stadium", "tour", "cricket").container;
    const retro = renderScenery("retro", "tour", "tennis").container;

    expect(scenes(stadium)).toEqual(["stadium-cricket-tour", "stadium-cricket-venue"]);
    expect(scenes(retro)).toEqual(["retro-tennis-tour", "retro-tennis-page"]);
  });

  it("draws one scene when both widths share it", () => {
    const { container } = renderScenery("retro", "page", "cricket");

    expect(scenes(container)).toEqual(["retro-cricket-page"]);
  });

  it("is decorative: hidden from screen readers and never takes a click", () => {
    const { container } = renderScenery("retro", "home");
    const layer = container.firstElementChild!;

    expect(layer).toHaveAttribute("aria-hidden", "true");
    expect(layer).toHaveClass("pointer-events-none");
  });

  it("draws nothing without a theme that has scenery", () => {
    expect(renderScenery("", "home").container).toBeEmptyDOMElement();
    expect(renderScenery("neon", "tour", "tennis").container).toBeEmptyDOMElement();
  });

  // Two copies of a scene's SVG (e.g. on two screens in a test, or the
  // same scene's gradients in both widths) mustn't share ids.
  it("gives each drawing its own SVG ids", () => {
    const { container } = render(
      <ThemeContext.Provider value="stadium">
        <Scenery kind="tour" family="cricket" />
        <Scenery kind="tour" family="cricket" />
      </ThemeContext.Provider>
    );
    const ids = Array.from(container.querySelectorAll("[id]")).map((el) => el.id);

    expect(ids.length).toBeGreaterThan(0);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.every((id) => !id.includes(":"))).toBe(true);
  });
});
