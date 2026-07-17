import { useEffect, useState } from "react";
import { fetchPlayerPool, startPracticeGame, submitGuess } from "../api/client";
import { PlayerSummary, GuessResponse, Difficulty } from "../types";
import PlayerSearch from "../components/PlayerSearch";
import ClueGrid from "../components/ClueGrid";
import ClueLegend from "../components/ClueLegend";
import ShareResult from "../components/ShareResult";

const MAX_GUESSES = 8;

interface Props {
  mode: Difficulty;
  onBackToHome: () => void;
}

export default function GameBoard({ mode, onBackToHome }: Props) {
  const [players, setPlayers] = useState<PlayerSummary[]>([]);
  const [sessionId, setSessionId] = useState<string | undefined>(undefined);
  const [guesses, setGuesses] = useState<GuessResponse[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [showLegend, setShowLegend] = useState(false);

  useEffect(() => {
    fetchPlayerPool().then(setPlayers).catch(() => setError("Couldn't load player list."));
  }, []);

  const startNewGame = async () => {
    setGuesses([]);
    setError("");
    setSessionId(undefined);

    if (mode !== "daily") {
      setLoading(true);
      try {
        const res = await startPracticeGame(mode);
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
  }, [mode]);

  const modeLabel = mode === "daily" ? "Daily" : mode[0].toUpperCase() + mode.slice(1);

  useEffect(() => {
    document.title = `ID the Tennis Player — ${modeLabel} | ID the Athlete`;
  }, [modeLabel]);

  const gameOver = guesses.length > 0 && (guesses[guesses.length - 1].isCorrect || guesses.length >= MAX_GUESSES);
  const won = guesses.length > 0 && guesses[guesses.length - 1].isCorrect;

  const handleGuess = async (player: PlayerSummary) => {
    if (gameOver) return;
    setError("");
    setLoading(true);
    try {
      const result = await submitGuess(player.id, mode, guesses.length + 1, sessionId);
      setGuesses((prev) => [...prev, result]);
    } catch {
      setError("Something went wrong submitting that guess.");
    } finally {
      setLoading(false);
    }
  };

  const guessedIds = new Set(
    guesses.map((g) => players.find((p) => p.name === g.guessedPlayerName)?.id).filter((id): id is number => id !== undefined)
  );

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">Tennis</span>
        <span className="text-[var(--text-primary)]"> Player</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-6">Guess the mystery ATP player in 8 tries</p>

      <div className="mt-8 flex flex-col items-center w-full">
        <PlayerSearch
          players={players}
          guessedIds={guessedIds}
          disabled={gameOver || loading}
          onGuess={handleGuess}
        />

        {error && <p className="text-[var(--accent-alt)] mt-2 text-sm">{error}</p>}

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

        <ClueGrid guesses={guesses} />

        {gameOver && (
          <div className="mt-6 text-center">
            <p className="text-[var(--text-primary)] text-lg font-semibold">
              {won ? "🎾 Nailed it !" : `The player was ${guesses[guesses.length - 1].answerName}`}
            </p>
            {guesses[guesses.length - 1].triviaBlurb && (
              <p className="text-[var(--text-muted)] text-xs italic mt-2 max-w-sm mx-auto">
                {guesses[guesses.length - 1].triviaBlurb}
              </p>
            )}
            <ShareResult guesses={guesses} won={won} mode={mode} />
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
    </div>
  );
}