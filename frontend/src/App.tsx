import { useEffect, useState } from "react";
import HomeScreen from "./pages/HomeScreen";
import TourSelect, { TENNIS_TOUR_CONFIG, CRICKET_TOUR_CONFIG } from "./pages/TourSelect";
import SportHome from "./pages/SportHome";
import PastChallenges from "./pages/PastChallenges";
import GameBoard from "./pages/GameBoard";
import { Sport, Difficulty } from "./types";
import { fetchActiveTheme } from "./api/client";

type Screen = "home" | "tourSelect" | "sportHome" | "pastChallenges" | "game";
type SportFamily = "tennis" | "cricket";

export default function App() {
  const [screen, setScreen] = useState<Screen>("home");
  // Which Home card was tapped -- picks which TourSelect config to render.
  // Null only while on the Home screen itself.
  const [sportFamily, setSportFamily] = useState<SportFamily | null>(null);
  const [sport, setSport] = useState<Sport | null>(null);
  const [mode, setMode] = useState<Difficulty | null>(null);
  // Set only when playing/replaying a specific past date via Past
  // Challenges; null means "today" (the normal Daily Challenge flow).
  const [dailyDate, setDailyDate] = useState<string | null>(null);
  const [themeReady, setThemeReady] = useState(false);

  useEffect(() => {
    fetchActiveTheme()
      .then((theme) => document.documentElement.setAttribute("data-theme", theme))
      .catch(() => document.documentElement.setAttribute("data-theme", "retro"))
      .finally(() => setThemeReady(true));
  }, []);

  const goHome = () => {
    setScreen("home");
    setSportFamily(null);
    setSport(null);
    setMode(null);
    setDailyDate(null);
  };

  const selectTennis = () => {
    setSportFamily("tennis");
    setScreen("tourSelect");
  };

  const selectCricket = () => {
    setSportFamily("cricket");
    setScreen("tourSelect");
  };

  // Back button from SportHome: return to this same family's tour-select
  // screen (sportFamily is already set from however we first got here, so
  // it's deliberately left untouched).
  const goToTourSelect = () => {
    setSport(null);
    setMode(null);
    setDailyDate(null);
    setScreen("tourSelect");
  };

  const selectTour = (s: Sport) => {
    setSport(s);
    setScreen("sportHome");
  };

  const selectMode = (m: Difficulty) => {
    setDailyDate(null);
    setMode(m);
    setScreen("game");
  };

  const openPastChallenges = () => {
    setScreen("pastChallenges");
  };

  const backToSportHomeFromPast = () => {
    setScreen("sportHome");
  };

  const selectPastDate = (date: string) => {
    setDailyDate(date);
    setMode("daily");
    setScreen("game");
  };

  // Back button from GameBoard: return to wherever this game session was
  // launched from -- Past Challenges for a past date, SportHome otherwise.
  const backToSportHome = () => {
    setMode(null);
    if (dailyDate !== null) {
      setDailyDate(null);
      setScreen("pastChallenges");
    } else {
      setScreen("sportHome");
    }
  };

  if (!themeReady) {
    return null;
  }

  if (screen === "tourSelect" && sportFamily) {
    const config = sportFamily === "cricket" ? CRICKET_TOUR_CONFIG : TENNIS_TOUR_CONFIG;
    return <TourSelect {...config} onSelectTour={selectTour} onBack={goHome} />;
  }

  if (screen === "sportHome" && sport) {
    return (
      <SportHome
        sport={sport}
        onSelectMode={selectMode}
        onPastChallenges={openPastChallenges}
        onBack={goToTourSelect}
      />
    );
  }

  if (screen === "pastChallenges" && sport) {
    return <PastChallenges sport={sport} onSelectDate={selectPastDate} onBack={backToSportHomeFromPast} />;
  }

  if (screen === "game" && sport && mode) {
    return (
      <GameBoard
        mode={mode}
        sportSlug={sport.slug}
        sportName={sport.name}
        dailyDate={dailyDate ?? undefined}
        onBackToHome={backToSportHome}
      />
    );
  }

  return <HomeScreen onSelectTennis={selectTennis} onSelectCricket={selectCricket} />;
}