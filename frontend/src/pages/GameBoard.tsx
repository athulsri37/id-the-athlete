import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { fetchPlayerPool, startPracticeGame, submitGuess, fetchCountryHint, fetchAttributeDefinitions } from "../api/client";
import { problemDetail, problemStatus } from "../api/problemDetails";
import { PlayerSummary, GuessResponse, Difficulty, AttributeDefinition, GameRef, Sport } from "../types";
import { familyDisplay } from "../config/sportFamilies";
import PlayerSearch from "../components/PlayerSearch";
import ClueGrid from "../components/ClueGrid";
import ClueLegend from "../components/ClueLegend";
import ShareResult from "../components/ShareResult";
import { recordDailyResult, getStreak, StreakData } from "../utils/dailyStreak";
import { getCompletionForDate, recordDailyCompletion, recordDailyTrivia } from "../utils/dailyCompletion";
import { getInProgress, saveInProgress, clearInProgress } from "../utils/dailyInProgress";
import { todayUtcDateString } from "../utils/localDate";
import { useGameTrivia } from "../hooks/useGameTrivia";
import { paths } from "../routes/paths";
import Scenery from "../components/scenery/Scenery";

interface Props {
  mode: Difficulty;
  sport: Sport;
  // Specific date (yyyy-MM-dd) to play/replay, daily mode only. Omitted
  // means today -- the normal Daily Challenge flow. Set only when arriving
  // via Past Challenges.
  dailyDate?: string;
  onBackToHome: () => void;
}

export default function GameBoard({ mode, sport, dailyDate, onBackToHome }: Props) {
  const { slug: sportSlug, name: sportName, family, maxGuesses, hintAfterGuess } = sport;
  const { winEmoji } = familyDisplay(family);
  const [players, setPlayers] = useState<PlayerSummary[]>([]);
  const [attributes, setAttributes] = useState<AttributeDefinition[]>([]);
  const [sessionId, setSessionId] = useState<string | undefined>(undefined);
  const [guesses, setGuesses] = useState<GuessResponse[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [showLegend, setShowLegend] = useState(false);

  const [showHintModal, setShowHintModal] = useState(false);
  const [hintOffered, setHintOffered] = useState(false);
  const [hintDeclined, setHintDeclined] = useState(false);
  const [revealedCountry, setRevealedCountry] = useState<string | null>(null);
  // A replayed Daily Challenge's trivia, saved with its completion.
  const [replayTriviaBlurb, setReplayTriviaBlurb] = useState<string | null>(null);

  const [streak, setStreak] = useState<StreakData | null>(null);
  const streakRecordedRef = useRef(false);

  // True when this date's Daily Challenge was already completed in a
  // previous visit and we're showing that result read-only instead of a
  // live game.
  const [isDailyReplay, setIsDailyReplay] = useState(false);

  // The actual calendar date this "daily" session is for. Today unless a
  // specific past date was supplied (via Past Challenges). Uses UTC "today"
  // (not browser-local) because the backend resolves the undated
  // ("today") daily puzzle from DateTime.UtcNow -- effectiveDate must agree
  // with that so completions get filed under the date whose puzzle was
  // actually played, not whatever day it happens to be in the visitor's
  // timezone.
  const effectiveDate = dailyDate ?? todayUtcDateString();
  // CRITICAL: this is the single source of truth for "must never touch the
  // streak." It's derived from the actual date being played, not from how
  // we got here, so it can't be bypassed by a navigation shortcut.
  const isPastDate = mode === "daily" && effectiveDate !== todayUtcDateString();

  useEffect(() => {
    fetchPlayerPool(sportSlug).then(setPlayers).catch(() => setError("Couldn't load player list."));
    fetchAttributeDefinitions(sportSlug).then(setAttributes).catch(() => setError("Couldn't load clue columns."));
  }, [sportSlug]);

  const startNewGame = async () => {
    setError("");
    setSessionId(undefined);
    setShowHintModal(false);
    setHintOffered(false);
    setHintDeclined(false);
    setRevealedCountry(null);
    setReplayTriviaBlurb(null);
    setStreak(null);
    setIsDailyReplay(false);
    streakRecordedRef.current = false;

    if (mode === "daily") {
      const completed = getCompletionForDate(sportSlug, effectiveDate);
      if (completed) {
        setGuesses(completed.guesses);
        setRevealedCountry(completed.revealedCountry);
        setReplayTriviaBlurb(completed.triviaBlurb ?? null);
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

      // Resume a game left part-way, e.g. by a refresh: same guesses, same
      // server-side game (its token is on the last guess).
      const saved = getInProgress(sportSlug, effectiveDate);
      if (saved) {
        setGuesses(saved.guesses);
        setRevealedCountry(saved.revealedCountry);
        setHintOffered(saved.hintOffered);
        setHintDeclined(saved.hintDeclined);
        return;
      }
    }

    setGuesses([]);

    if (mode !== "daily") {
      setLoading(true);
      try {
        const res = await startPracticeGame(sportSlug, mode);
        setSessionId(res.sessionId);
      } catch (err) {
        setError(problemDetail(err, "Couldn't start a new game. Try again."));
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
    document.title = `ID the ${sportName} Player: ${modeLabel}${dateSuffix} | ID the Athlete`;
  }, [modeLabel, sportName, isPastDate, effectiveDate]);

  const gameOver = guesses.length > 0 && (guesses[guesses.length - 1].isCorrect || guesses.length >= maxGuesses);
  const won = guesses.length > 0 && guesses[guesses.length - 1].isCorrect;
  // Daily games only: the server's signed record of this game's guess
  // count, always taken from the latest guess and sent back with the next
  // guess or hint request.
  const gameToken = guesses.length > 0 ? guesses[guesses.length - 1].gameToken ?? undefined : undefined;
  const gameRef: GameRef = { mode, sessionId, date: isPastDate ? effectiveDate : undefined, gameToken };

  // Offer the country hint once, right after the 5th guess, unless the
  // player already matched the country naturally or the game just ended.
  useEffect(() => {
    if (guesses.length !== hintAfterGuess || hintOffered || gameOver) return;

    const countryAlreadyMatched = guesses.some((g) => g.clues.some((c) => c.attributeKey === "country" && c.isMatch));
    if (!countryAlreadyMatched) {
      setShowHintModal(true);
    }
    setHintOffered(true);
  }, [guesses, hintOffered, gameOver, hintAfterGuess]);

  // The puzzle this game is for, as the server says once the first guess
  // is in; effectiveDate until then. They differ only for a game still
  // going after midnight UTC: that's still the earlier day's puzzle (its
  // token pins it), so everything about the game -- the save, the
  // completion, the streak, the trivia -- is filed under gameDate, never
  // under the page's "today".
  const gameDate = guesses[0]?.puzzleDate ?? effectiveDate;

  // Save a live daily game after every change, so a refresh resumes it.
  // Once it's over it's filed as a completion instead (below). Under
  // gameDate, a game still going after midnight UTC isn't resumed as
  // today's: a refresh starts today's, and Past Challenges resumes it.
  useEffect(() => {
    if (mode !== "daily" || isDailyReplay || guesses.length === 0 || gameOver) return;
    saveInProgress(sportSlug, { date: gameDate, guesses, revealedCountry, hintOffered, hintDeclined });
  }, [mode, isDailyReplay, guesses, gameOver, sportSlug, gameDate, revealedCountry, hintOffered, hintDeclined]);

  // Mark this date's puzzle as completed exactly once per finished game,
  // and -- only for genuine "today" plays -- update the streak. Guarded by
  // a ref (not just state) so a re-render after gameOver is already true
  // can't record the same result twice, and so restoring an
  // already-completed game (see startNewGame) doesn't re-record it.
  useEffect(() => {
    if (mode !== "daily" || !gameOver || streakRecordedRef.current) return;
    streakRecordedRef.current = true;

    recordDailyCompletion(sportSlug, gameDate, won, guesses, revealedCountry);
    clearInProgress(sportSlug);

    // CRITICAL: streak logic must only ever fire for today's Daily
    // Challenge. Catching up on a past day via Past Challenges must never
    // affect it: isPastDate is true for those. A game opened as today's and
    // finished after midnight UTC still counts, for its own puzzle date
    // (gameDate), so it extends the streak instead of skipping a day.
    if (!isPastDate) {
      setStreak(recordDailyResult(sportSlug, gameDate, won));
    }
  }, [mode, gameOver, won, sportSlug, guesses, revealedCountry, isPastDate, gameDate]);

  // A live game's trivia arrives after the result is showing; a daily one
  // is saved with its completion so a replay shows it without refetching.
  const triviaToken = gameOver && !isDailyReplay ? guesses[guesses.length - 1].triviaToken : null;
  const liveTriviaBlurb = useGameTrivia(
    sportSlug,
    triviaToken,
    mode === "daily" ? (blurb) => recordDailyTrivia(sportSlug, gameDate, blurb) : undefined
  );
  const triviaBlurb = isDailyReplay ? replayTriviaBlurb : liveTriviaBlurb;

  const handleGuess = async (player: PlayerSummary) => {
    if (gameOver) return;
    setError("");
    setLoading(true);
    try {
      const result = await submitGuess(sportSlug, player.id, gameRef);
      setGuesses((prev) => [...prev, result]);
    } catch (err) {
      // The server's message when it refused the guess, e.g. an expired
      // practice game; the generic one for a network error or a 500.
      setError(problemDetail(err, "Something went wrong submitting that guess."));
      // A refused daily guess usually means the saved game can't continue
      // (its token no longer verifies, e.g. after the signing key changed).
      // Forget it, so a refresh starts a fresh game instead of resuming
      // this one again.
      if (mode === "daily" && problemStatus(err) === 400) clearInProgress(sportSlug);
    } finally {
      setLoading(false);
    }
  };

  const handleHintYes = async () => {
    try {
      const country = await fetchCountryHint(sportSlug, gameRef);
      setRevealedCountry(country);
      setHintDeclined(false);
    } catch (err) {
      setError(problemDetail(err, "Couldn't fetch the hint right now."));
    } finally {
      setShowHintModal(false);
    }
  };

  const handleHintNo = () => {
    setShowHintModal(false);
    setHintDeclined(true);
  };

  // Players already guessed, left out of the search. By ID, so it doesn't
  // depend on names staying unique and unchanged (e.g. a curator correcting
  // a name in the database mid-game); guesses saved before responses
  // carried the ID fall back to the name.
  const guessedIds = new Set(
    guesses
      .map((g) => g.guessedPlayerId ?? players.find((p) => p.name === g.guessedPlayerName)?.id)
      .filter((id): id is number => id !== undefined)
  );

  return (
    <div className="isolate min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <Scenery kind="game" family={sport.family} />
      <h1 className="font-heading text-3xl sm:text-4xl lg:text-5xl text-center text-balance tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">{sportName}</span>
        <span className="text-[var(--text-primary)]"> Player</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-1">Guess the mystery player in {maxGuesses} tries</p>
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
          hintDeclined &&
          !gameOver && (
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
            {guesses.length} / {maxGuesses} guesses
          </p>
          <button
            onClick={() => setShowLegend((v) => !v)}
            aria-label="What do the clues mean?"
            className="btn-card relative flex items-center justify-center w-5 h-5 rounded-full text-xs font-bold leading-none before:absolute before:-inset-3 before:content-['']"
          >
            ?
          </button>
        </div>

        {showLegend && (
          <div className="card rounded-md p-5 mt-3 w-full max-w-md text-left">
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-2">
              What do the clues mean?
            </h2>
            <ClueLegend family={family} />
            <Link
              to={paths.howToPlay(family)}
              className="inline-block mt-3 text-sm text-[var(--link-accent)] underline hover:text-[var(--link-accent-hover)]"
            >
              All the rules: How to play
            </Link>
          </div>
        )}

        <ClueGrid guesses={guesses} revealedCountry={revealedCountry} attributes={attributes} />

        {gameOver && (
          <div className="mt-6 text-center">
            {isDailyReplay && (
              <p className="text-[var(--text-muted)] text-xs italic mb-2">
                {isPastDate
                  ? `Here's your result from ${effectiveDate}. This doesn't affect your streak.`
                  : "You've already completed today's Daily Challenge. Here's your result."}
              </p>
            )}
            <p className="text-[var(--text-primary)] text-lg font-semibold">
              {won ? `${winEmoji ? `${winEmoji} ` : ""}Nailed it!` : `The player was ${guesses[guesses.length - 1].answerName}`}
            </p>
            {triviaBlurb && (
              <div className="mt-3 max-w-sm mx-auto flex items-start gap-3 text-left rounded-md border-l-4 border-[var(--accent)] bg-[var(--bg-card)] px-4 py-3">
                <span
                  className="flex-shrink-0 flex items-center justify-center w-6 h-6 text-lg"
                  aria-hidden="true"
                >
                  💡
                </span>
                <div>
                  <p className="text-[var(--accent)] text-[11px] font-bold uppercase tracking-wider mb-1">
                    Did you know?
                  </p>
                  <p className="text-[var(--text-primary)] text-sm leading-relaxed text-justify">
                    {triviaBlurb}
                  </p>
                </div>
              </div>
            )}
            {mode === "daily" && !isPastDate && streak && (
              <p className="text-[var(--text-primary)] text-sm font-semibold mt-2">
                🔥 {streak.currentStreak} day streak
                {streak.maxStreak > 0 && (
                  <span className="text-[var(--text-muted)] font-normal"> · Best streak: {streak.maxStreak}</span>
                )}
              </p>
            )}
            <ShareResult guesses={guesses} won={won} mode={mode} sportName={sportName} maxGuesses={maxGuesses} />
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