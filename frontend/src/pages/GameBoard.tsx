import { useEffect, useState } from "react";
import { fetchPlayerPool, startPracticeGame, submitGuess } from "../api/client";
import { PlayerSummary, GuessResponse, Difficulty } from "../types";
import PlayerSearch from "../components/PlayerSearch";
import ClueGrid from "../components/ClueGrid";
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
    <div className="min-h-screen bg-court-green flex flex-col items-center px-4 py-10">
      <h1 className="font-display text-5xl tracking-wide text-ace-500 mb-1">ID the Tennis Player</h1>
      <p className="text-court-chalk/70 text-sm mb-6">Guess the mystery ATP player in 8 tries</p>

      <div className="mt-8 flex flex-col items-center w-full">
        <PlayerSearch
          players={players}
          guessedIds={guessedIds}
          disabled={gameOver || loading}
          onGuess={handleGuess}
        />

        {error && <p className="text-court-clay mt-2 text-sm">{error}</p>}

        <p className="text-court-chalk/60 text-xs mt-2">
          {guesses.length} / {MAX_GUESSES} guesses
        </p>

        <ClueGrid guesses={guesses} />

        {gameOver && (
          <div className="mt-6 text-center">
            <p className="text-court-chalk text-lg font-semibold">
              {won ? "🎾 Nice read!" : `The player was ${guesses[guesses.length - 1].answerName}`}
            </p>
            {guesses[guesses.length - 1].triviaBlurb && (
              <p className="text-court-chalk/60 text-xs italic mt-2 max-w-sm mx-auto">
                {guesses[guesses.length - 1].triviaBlurb}
              </p>
            )}
            <ShareResult guesses={guesses} won={won} mode={mode} />
            <div className="flex items-center justify-center gap-4 mt-3">
              {mode !== "daily" && (
                <button
                  onClick={() => startNewGame()}
                  className="text-sm text-court-chalk/70 underline hover:text-court-chalk"
                >
                  Play another
                </button>
              )}
              <button
                onClick={onBackToHome}
                className="text-sm text-court-chalk/70 underline hover:text-court-chalk"
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