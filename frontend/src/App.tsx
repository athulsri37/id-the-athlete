import { useEffect, useState } from "react";
import HomeScreen from "./pages/HomeScreen";
import TourSelect from "./pages/TourSelect";
import SportHome from "./pages/SportHome";
import GameBoard from "./pages/GameBoard";
import { Sport, Difficulty } from "./types";
import { fetchActiveTheme } from "./api/client";

type Screen = "home" | "tourSelect" | "sportHome" | "game";

export default function App() {
  const [screen, setScreen] = useState<Screen>("home");
  const [sport, setSport] = useState<Sport | null>(null);
  const [mode, setMode] = useState<Difficulty | null>(null);
  const [themeReady, setThemeReady] = useState(false);

  useEffect(() => {
    fetchActiveTheme()
      .then((theme) => document.documentElement.setAttribute("data-theme", theme))
      .catch(() => document.documentElement.setAttribute("data-theme", "retro"))
      .finally(() => setThemeReady(true));
  }, []);

  const goHome = () => {
    setScreen("home");
    setSport(null);
    setMode(null);
  };

  const selectTennis = () => {
    setScreen("tourSelect");
  };

  const goToTourSelect = () => {
    setSport(null);
    setMode(null);
    setScreen("tourSelect");
  };

  const selectTour = (s: Sport) => {
    setSport(s);
    setScreen("sportHome");
  };

  const selectMode = (m: Difficulty) => {
    setMode(m);
    setScreen("game");
  };

  const backToSportHome = () => {
    setMode(null);
    setScreen("sportHome");
  };

  if (!themeReady) {
    return null;
  }

  if (screen === "tourSelect") {
    return <TourSelect onSelectTour={selectTour} onBack={goHome} />;
  }

  if (screen === "sportHome" && sport) {
    return <SportHome sport={sport} onSelectMode={selectMode} onBack={goToTourSelect} />;
  }

  if (screen === "game" && sport && mode) {
    return <GameBoard mode={mode} sportSlug={sport.slug} sportName={sport.name} onBackToHome={backToSportHome} />;
  }

  return <HomeScreen onSelectTennis={selectTennis} />;
}