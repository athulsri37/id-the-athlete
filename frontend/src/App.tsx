import { useEffect, useState } from "react";
import { Route, Routes, useLocation, useNavigate, useParams } from "react-router";
import HomeScreen from "./pages/HomeScreen";
import TourSelect from "./pages/TourSelect";
import SportHome from "./pages/SportHome";
import PastChallenges from "./pages/PastChallenges";
import GameBoard from "./pages/GameBoard";
import NotFound from "./pages/NotFound";
import HowToPlay from "./pages/HowToPlay";
import ErrorBoundary from "./components/ErrorBoundary";
import { Sport } from "./types";
import { fetchActiveTheme, fetchDailyPuzzleDates, fetchSports } from "./api/client";
import { groupByFamily } from "./config/sportFamilies";
import { findTour, isMode, isPuzzleDate, paths } from "./routes/paths";

// Every tour from the API, which every screen's address is resolved
// against. sports is null until loaded.
interface SportsData {
  sports: Sport[] | null;
  loadFailed: boolean;
}

// One route per screen; the addresses are described in routes/paths.
export default function App() {
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [themeReady, setThemeReady] = useState(false);

  useEffect(() => {
    fetchActiveTheme()
      .then((theme) => document.documentElement.setAttribute("data-theme", theme))
      .catch(() => document.documentElement.setAttribute("data-theme", "retro"))
      .finally(() => setThemeReady(true));

    fetchSports()
      .then(setSports)
      .catch(() => setLoadFailed(true));
  }, []);

  if (!themeReady) {
    return null;
  }

  const data: SportsData = { sports, loadFailed };
  return (
    <ErrorBoundary>
      <Routes>
        <Route path="/" element={<HomePage {...data} />} />
        <Route path="/how-to-play" element={<HowToPlayPage {...data} />} />
        <Route path="/:family" element={<FamilyPage {...data} />} />
        <Route path="/:family/:tour" element={<TourPage {...data} />} />
        <Route path="/:family/:tour/past" element={<PastPage {...data} />} />
        <Route path="/:family/:tour/past/:date" element={<PastGamePage {...data} />} />
        <Route path="/:family/:tour/:mode" element={<GamePage {...data} />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </ErrorBoundary>
  );
}

function HomePage({ sports, loadFailed }: SportsData) {
  const navigate = useNavigate();
  return (
    <HomeScreen
      families={sports && groupByFamily(sports)}
      loadFailed={loadFailed}
      onSelectFamily={(family) => navigate(paths.family(family))}
      onHowToPlay={() => navigate(paths.howToPlay())}
    />
  );
}

// Back returns to wherever the reader came from; opened directly, home.
function HowToPlayPage(data: SportsData) {
  const navigate = useNavigate();
  const { key } = useLocation();
  return (
    <WaitForSports {...data}>
      {(sports) =>
        sports.length > 0 ? (
          <HowToPlay sports={sports} onBack={() => (key === "default" ? navigate(paths.home()) : navigate(-1))} />
        ) : (
          <NotFound />
        )
      }
    </WaitForSports>
  );
}

// Until the tours load, a deep link shows nothing (it's brief); if they
// can't load, the home screen says so.
function WaitForSports({ sports, loadFailed, children }: SportsData & { children: (sports: Sport[]) => JSX.Element }) {
  if (loadFailed) return <HomePage sports={null} loadFailed />;
  if (!sports) return null;
  return children(sports);
}

function FamilyPage(data: SportsData) {
  const { family = "" } = useParams();
  const navigate = useNavigate();
  return (
    <WaitForSports {...data}>
      {(sports) => {
        const group = groupByFamily(sports).find((g) => g.family === family);
        if (!group) return <NotFound />;
        return <TourSelect group={group} onSelectTour={(s) => navigate(paths.tour(s))} onBack={() => navigate(paths.home())} />;
      }}
    </WaitForSports>
  );
}

// The tour named by /:family/:tour, or NotFound.
function WithTour({ children, ...data }: SportsData & { children: (sport: Sport) => JSX.Element }) {
  const { family = "", tour = "" } = useParams();
  return (
    <WaitForSports {...data}>
      {(sports) => {
        const sport = findTour(sports, family, tour);
        return sport ? children(sport) : <NotFound />;
      }}
    </WaitForSports>
  );
}

function TourPage(data: SportsData) {
  const navigate = useNavigate();
  return (
    <WithTour {...data}>
      {(sport) => (
        <SportHome
          sport={sport}
          onSelectMode={(mode) => navigate(paths.game(sport, mode))}
          onPastChallenges={() => navigate(paths.past(sport))}
          onHowToPlay={() => navigate(paths.howToPlay(sport.family))}
          onBack={() => navigate(paths.family(sport.family))}
        />
      )}
    </WithTour>
  );
}

function PastPage(data: SportsData) {
  const navigate = useNavigate();
  return (
    <WithTour {...data}>
      {(sport) => (
        <PastChallenges
          sport={sport}
          onSelectDate={(date) => navigate(paths.pastGame(sport, date))}
          onBack={() => navigate(paths.tour(sport))}
        />
      )}
    </WithTour>
  );
}

// Keyed by address so every game is a fresh GameBoard: switching games
// never carries one game's state into the next.
function GamePage(data: SportsData) {
  const { mode = "" } = useParams();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  if (!isMode(mode)) return <NotFound />;
  return (
    <WithTour {...data}>
      {(sport) => <GameBoard key={pathname} mode={mode} sport={sport} onBackToHome={() => navigate(paths.tour(sport))} />}
    </WithTour>
  );
}

function PastGamePage(data: SportsData) {
  const { date = "" } = useParams();
  return (
    <WithTour {...data}>
      {(sport) => (isPuzzleDate(date) ? <PastGame sport={sport} date={date} /> : <NotFound />)}
    </WithTour>
  );
}

// A Past Challenge only exists for a date that has a puzzle, so an old or
// mistyped date shows NotFound rather than a game that can't be played. If
// the list itself can't be loaded, the game is shown and the server decides.
function PastGame({ sport, date }: { sport: Sport; date: string }) {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const [exists, setExists] = useState<boolean | null>(null);

  useEffect(() => {
    let current = true;
    setExists(null);
    fetchDailyPuzzleDates(sport.slug)
      .then((dates) => current && setExists(dates.includes(date)))
      .catch(() => current && setExists(true));
    return () => {
      current = false;
    };
  }, [sport.slug, date]);

  if (exists === null) return null;
  if (!exists) return <NotFound />;
  return (
    <GameBoard key={pathname} mode="daily" sport={sport} dailyDate={date} onBackToHome={() => navigate(paths.past(sport))} />
  );
}
