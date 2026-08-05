import { useEffect, useRef, useState } from "react";
import { fetchPlayerPool, startPracticeGame, submitGuess, fetchCountryHint } from "../api/client";
import { PlayerSummary, GuessResponse, Difficulty } from "../types";
import PlayerSearch from "../components/PlayerSearch";
import ClueGrid from "../components/ClueGrid";
import ClueLegend from "../components/ClueLegend";
import ShareResult from "../components/ShareResult";
import { recordDailyResult, getStreak, StreakData } from "../utils/dailyStreak";
import { getCompletionForDate, recordDailyCompletion } from "../utils/dailyCompletion";
import { todayLocalDateString } from "../utils/localDate";

const MAX_GUESSES = 8;
const HINT_AFTER_GUESS = 5;

interface Props {
  mode: Difficulty;
  sportSlug: string;
  sportName: string;
  // Specific date (yyyy-MM-dd) to play/replay, daily mode only. Omitted
  // means today -- the normal Daily Challenge flow. Set only when arriving
  // via Past Challenges.
  dailyDate?: string;
  onBackToHome: () => void;
}

export default function GameBoard({ mode, sportSlug, sportName, dailyDate, onBackToHome }: Props) {
  const [players, setPlayers] = useState<PlayerSummary[]>([]);
  const [sessionId, setSessionId] = useState<string | undefined>(undefined);
  const [guesses, setGuesses] = useState<GuessResponse[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [showLegend, setShowLegend] = useState(false);

  const [showHintModal, setShowHintModal] = useState(false);
  const [hintOffered, setHintOffered] = useState(false);
  const [hintDeclined, setHintDeclined] = useState(false);
  const [revealedCountry, setRevealedCountry] = useState<string | null>(null);

  const [streak, setStreak] = useState<StreakData | null>(null);
  const streakRecordedRef = useRef(false);

  // True when this date's Daily Challenge was already completed in a
  // previous visit and we're showing that result read-only instead of a
  // live game.
  const [isDailyReplay, setIsDailyReplay] = useState(false);

  // The actual calendar date this "daily" session is for. Today unless a
  // specific past date was supplied (via Past Challenges).
  const effectiveDate = dailyDate ?? todayLocalDateString();
  // CRITICAL: this is the single source of truth for "must never touch the
  // streak." It's derived from the actual date being played, not from how
  // we got here, so it can't be bypassed by a navigation shortcut.
  const isPastDate = mode === "daily" && effectiveDate !== todayLocalDateString();

  useEffect(() => {
    fetchPlayerPool(sportSlug).then(setPlayers).catch(() => setError("Couldn't load player list."));
  }, [sportSlug]);

  const startNewGame = async () => {
    setError("");
    setSessionId(undefined);
    setShowHintModal(false);
    setHintOffered(false);
    setHintDeclined(false);
    setRevealedCountry(null);
    setStreak(null);
    setIsDailyReplay(false);
    streakRecordedRef.current = false;

    if (mode === "daily") {
      const completed = getCompletionForDate(sportSlug, effectiveDate);
      if (completed) {
        setGuesses(completed.guesses);
        setRevealedCountry(completed.revealedCountry);
        // Never surface a streak for a past-date replay -- that day never
        // contributed to it, showing a number here would be misleading.
        if (!isPastDate) {
          setStreak(getStreak(sportSlug));
        }
        setHintOffered(true);
        setIsDailyReplay(true);
        streakRecordedRef.current = true;
        return;
      }
    }

    setGuesses([]);

    if (mode !== "daily") {
      setLoading(true);
      try {
        const res = await startPracticeGame(sportSlug, mode);
        setSessionId(res.sessionId);
      } catch {
        setError("Couldn't start a new game. Try again.");
      } finally {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    startNewGame();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, dailyDate]);

  const modeLabel = mode === "daily" ? "Daily" : mode[0].toUpperCase() + mode.slice(1);

  useEffect(() => {
    const dateSuffix = isPastDate ? ` (${effectiveDate})` : "";
    document.title = `ID the ${sportName} Player — ${modeLabel}${dateSuffix} | ID the Athlete`;
  }, [modeLabel, sportName, isPastDate, effectiveDate]);

  const gameOver = guesses.length > 0 && (guesses[guesses.length - 1].isCorrect || guesses.length >= MAX_GUESSES);
  const won = guesses.length > 0 && guesses[guesses.length - 1].isCorrect;

  // Offer the country hint once, right after the 5th guess, unless the
  // player already matched the country naturally or the game just ended.
  useEffect(() => {
    if (guesses.length !== HINT_AFTER_GUESS || hintOffered || gameOver) return;

    const countryAlreadyMatched = guesses.some((g) => g.clues.some((c) => c.attributeKey === "country" && c.isMatch));
    if (!countryAlreadyMatched) {
      setShowHintModal(true);
    }
    setHintOffered(true);
  }, [guesses, hintOffered, gameOver]);

  // Mark this date's puzzle as completed exactly once per finished game,
  // and -- only for genuine "today" plays -- update the streak. Guarded by
  // a ref (not just state) so a re-render after gameOver is already true
  // can't record the same result twice, and so restoring an
  // already-completed game (see startNewGame) doesn't re-record it.
  useEffect(() => {
    if (mode !== "daily" || !gameOver || streakRecordedRef.current) return;
    streakRecordedRef.current = true;

    recordDailyCompletion(sportSlug, effectiveDate, won, guesses, revealedCountry);

    // CRITICAL: streak logic must only ever fire for today. Catching up on
    // a past day via Past Challenges must never affect it.
    if (!isPastDate) {
      setStreak(recordDailyResult(sportSlug, won));
    }
  }, [mode, gameOver, won, sportSlug, guesses, revealedCountry, isPastDate, effectiveDate]);

  const handleGuess = async (player: PlayerSummary) => {
    if (gameOver) return;
    setError("");
    setLoading(true);
    try {
      const result = await submitGuess(sportSlug, player.id, mode, guesses.length + 1, sessionId, isPastDate ? effectiveDate : undefined);
      setGuesses((prev) => [...prev, result]);
    } catch {
      setError("Something went wrong submitting that guess.");
    } finally {
      setLoading(false);
    }
  };

  const handleHintYes = async () => {
    try {
      const country = await fetchCountryHint(sportSlug, mode, sessionId, isPastDate ? effectiveDate : undefined);
      setRevealedCountry(country);
      setHintDeclined(false);
    } catch {
      setError("Couldn't fetch the hint right now.");
    } finally {
      setShowHintModal(false);
    }
  };

  const handleHintNo = () => {
    setShowHintModal(false);
    setHintDeclined(true);
  };

  const guessedIds = new Set(
    guesses.map((g) => players.find((p) => p.name === g.guessedPlayerName)?.id).filter((id): id is number => id !== undefined)
  );

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">{sportName}</span>
        <span className="text-[var(--text-primary)]"> Player</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-1">Guess the mystery tennis player in 8 tries</p>
      {isPastDate && (
        <p className="text-[var(--text-muted)] text-xs mb-5">
          Playing:{" "}
          {new Date(`${effectiveDate}T00:00:00`).toLocaleDateString(undefined, {
            weekday: "long",
            month: "short",
            day: "numeric",
          })}
        </p>
      )}
      {!isPastDate && <div className="mb-5" />}

      <div className="mt-3 flex flex-col items-center w-full">
        <PlayerSearch
          players={players}
          guessedIds={guessedIds}
          disabled={gameOver || loading}
          placeholderOverride={isDailyReplay && !isPastDate ? "Come back tomorrow!" : undefined}
          onGuess={handleGuess}
        />

        {error && <p className="text-[var(--accent-alt)] mt-2 text-sm">{error}</p>}

        {revealedCountry !== null ? (
          <div className="card mt-3 px-4 py-1.5 rounded-full text-xs font-semibold text-[var(--text-primary)]">
            Country: {revealedCountry}
          </div>
        ) : (
          hintDeclined && (
            <button
              onClick={() => setShowHintModal(true)}
              className="btn-card animate-hint-pulse mt-3 px-4 py-1.5 rounded-full text-xs font-semibold"
            >
              Hint available
            </button>
          )
        )}

        <div className="flex items-center gap-2 mt-2">
          <p className="text-[var(--text-muted)] text-xs">
            {guesses.length} / {MAX_GUESSES} guesses
          </p>
          <button
            onClick={() => setShowLegend((v) => !v)}
            aria-label="What do the clues mean?"
            className="btn-card flex items-center justify-center w-5 h-5 rounded-full text-xs font-bold leading-none"
          >
            ?
          </button>
        </div>

        {showLegend && (
          <div className="card rounded-md p-5 mt-3 w-full max-w-md text-left">
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-2">
              What do the clues mean?
            </h2>
            <ClueLegend />
          </div>
        )}

        <ClueGrid guesses={guesses} revealedCountry={revealedCountry} />

        {gameOver && (
          <div className="mt-6 text-center">
            {isDailyReplay && (
              <p className="text-[var(--text-muted)] text-xs italic mb-2">
                {isPastDate
                  ? `Here's your result from ${effectiveDate} — this doesn't affect your streak.`
                  : "You've already completed today's Daily Challenge — here's your result."}
              </p>
            )}
            <p className="text-[var(--text-primary)] text-lg font-semibold">
              {won ? "🎾 Nailed it !" : `The player was ${guesses[guesses.length - 1].answerName}`}
            </p>
            {guesses[guesses.length - 1].triviaBlurb && (
              <p className="text-[var(--text-muted)] text-xs italic mt-2 max-w-sm mx-auto">
                {guesses[guesses.length - 1].triviaBlurb}
              </p>
            )}
            {mode === "daily" && !isPastDate && streak && (
              <p className="text-[var(--text-primary)] text-sm font-semibold mt-2">
                🔥 {streak.currentStreak} day streak
                {streak.maxStreak > 0 && (
                  <span className="text-[var(--text-muted)] font-normal"> · Best streak: {streak.maxStreak}</span>
                )}
              </p>
            )}
            <ShareResult guesses={guesses} won={won} mode={mode} sportName={sportName} />
            <div className="flex items-center justify-center gap-4 mt-3">
              {mode !== "daily" && (
                <button
                  onClick={() => startNewGame()}
                  className="text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)]"
                >
                  Play another
                </button>
              )}
              <button
                onClick={onBackToHome}
                className="text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)]"
              >
                Back to Home
              </button>
            </div>
          </div>
        )}
      </div>

      {showHintModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
          <div className="card rounded-md p-6 max-w-sm w-full text-center">
            <p className="text-[var(--text-primary)] text-base font-semibold mb-5">
              Want a hint? Reveal the mystery player's country?
            </p>
            <div className="flex items-center justify-center gap-3">
              <button onClick={handleHintYes} className="btn-card px-6 py-2 rounded-md font-semibold">
                Yes
              </button>
              <button onClick={handleHintNo} className="btn-card px-6 py-2 rounded-md font-semibold">
                No
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}