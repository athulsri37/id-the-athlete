import { describe, expect, it } from "vitest";
import { sceneFor } from "./scenes";
import {
  RetroCricketCorner,
  RetroCricketPage,
  RetroHome,
  RetroPage,
  RetroTennisPage,
  RetroTennisTour,
} from "./retro/RetroScenes";
import {
  StadiumCricketTour,
  StadiumCricketVenue,
  StadiumHome,
  StadiumPage,
  StadiumTennisPage,
} from "./stadium/StadiumScenes";

describe("sceneFor", () => {
  it("gives the home screen its full scene, and phones the general one", () => {
    expect(sceneFor("retro", "home")).toEqual({ wide: RetroHome, narrow: RetroPage });
    expect(sceneFor("stadium", "home")).toEqual({ wide: StadiumHome, narrow: StadiumPage });
  });

  it("gives a sport's screens its full scene, and phones its venue or, without one, its faint one", () => {
    expect(sceneFor("stadium", "tour", "cricket")).toEqual({ wide: StadiumCricketTour, narrow: StadiumCricketVenue });
    expect(sceneFor("retro", "tour", "tennis")).toEqual({ wide: RetroTennisTour, narrow: RetroTennisPage });
  });

  it("gives reading screens the faint scene at every width", () => {
    expect(sceneFor("retro", "page", "cricket")).toEqual({ wide: RetroCricketPage, narrow: RetroCricketPage });
    expect(sceneFor("stadium", "page")).toEqual({ wide: StadiumPage, narrow: StadiumPage });
  });

  it("gives a game the faint scene, but only a corner piece on a phone", () => {
    expect(sceneFor("retro", "game", "cricket")).toEqual({ wide: RetroCricketPage, narrow: RetroCricketCorner });
    expect(sceneFor("stadium", "game", "tennis")).toEqual({ wide: StadiumTennisPage, narrow: StadiumPage });
    expect(sceneFor("retro", "game", "football")).toEqual({ wide: RetroPage, narrow: RetroPage });
  });

  it("falls back to the theme's general scene for a sport without its own", () => {
    expect(sceneFor("retro", "tour", "football")).toEqual({ wide: RetroPage, narrow: RetroPage });
    expect(sceneFor("stadium", "page", "football")).toEqual({ wide: StadiumPage, narrow: StadiumPage });
  });

  it("draws nothing for a theme without scenery", () => {
    expect(sceneFor("neon", "home")).toBeNull();
    expect(sceneFor("", "tour", "tennis")).toBeNull();
  });
});
