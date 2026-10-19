import { ComponentType } from "react";
import {
  RetroCricketPage,
  RetroCricketTour,
  RetroHome,
  RetroPage,
  RetroTennisPage,
  RetroTennisTour,
} from "./retro/RetroScenes";
import {
  StadiumCricketPage,
  StadiumCricketTour,
  StadiumHome,
  StadiumPage,
  StadiumTennisPage,
  StadiumTennisTour,
} from "./stadium/StadiumScenes";

// Which background artwork each screen gets, per theme and sport family.
//   home: the home screen.
//   tour: a sport's own screens (choosing a tour, a tour's menu).
//   page: screens with more to read (a game, Past Challenges, How to Play,
//         not found), which get a faint version kept to the edges.
// Phones and portrait tablets (under 1024px) always get the faint version:
// a full scene would sit behind the buttons there. A family or theme without
// artwork of its own gets the theme's general page scenery, or none.
export type SceneKind = "home" | "tour" | "page";

type Scene = ComponentType;

interface ThemeScenes {
  home: Scene;
  tours: Partial<Record<string, Scene>>;
  pages: Partial<Record<string, Scene>>;
  page: Scene; // any screen without a family, or a family without its own
}

const SCENES: Partial<Record<string, ThemeScenes>> = {
  retro: {
    home: RetroHome,
    tours: { tennis: RetroTennisTour, cricket: RetroCricketTour },
    pages: { tennis: RetroTennisPage, cricket: RetroCricketPage },
    page: RetroPage,
  },
  stadium: {
    home: StadiumHome,
    tours: { tennis: StadiumTennisTour, cricket: StadiumCricketTour },
    pages: { tennis: StadiumTennisPage, cricket: StadiumCricketPage },
    page: StadiumPage,
  },
};

export interface SceneChoice {
  wide: Scene; // 1024px wide and up
  narrow: Scene; // phones and portrait tablets
}

export function sceneFor(theme: string, kind: SceneKind, family?: string): SceneChoice | null {
  const scenes = SCENES[theme];
  if (!scenes) return null;

  const page = (family && scenes.pages[family]) || scenes.page;
  const wide = kind === "home" ? scenes.home : kind === "tour" ? (family && scenes.tours[family]) || page : page;
  return { wide, narrow: page };
}
