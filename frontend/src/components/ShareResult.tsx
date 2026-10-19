import { useEffect, useRef, useState } from "react";
import { Difficulty, GuessResponse, Sport } from "../types";
import { paths } from "../routes/paths";

interface ShareInput {
  guesses: GuessResponse[];
  won: boolean;
  sport: Sport;
  mode: Difficulty;
  // The puzzle's date (yyyy-MM-dd) for a daily game; ignored for practice.
  puzzleDate?: string;
  // A Past Challenge rather than today's puzzle.
  isPastDate: boolean;
  winEmoji?: string;
  // The country the hint revealed: it shows as a match, as in the grid.
  revealedCountry: string | null;
  // Where the game is served, e.g. https://id-the-athlete.onrender.com.
  origin: string;
}

const MODE_LABEL: Record<Difficulty, string> = { daily: "Daily", easy: "Easy", medium: "Medium", hard: "Hard" };

// "2026-10-19" -> "19 Oct 2026", the same everywhere (the date is a UTC day).
function formatPuzzleDate(date: string): string {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day)).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
}

// The spoiler-free result: which game it was, the score, one row of squares
// per guess (green match, yellow close, white miss, as in the grid), and a
// link to the same game: that day's puzzle for a daily game, the same mode
// for practice.
export function buildShareText(input: ShareInput): string {
  const { guesses, won, sport, mode, puzzleDate, isPastDate, winEmoji, revealedCountry, origin } = input;
  const max = sport.maxGuesses;
  const score = won ? `${winEmoji ? `${winEmoji} ` : ""}${guesses.length}/${max}` : `X/${max}`;

  const isDaily = mode === "daily" && puzzleDate !== undefined;
  const which = isDaily
    ? `${isPastDate ? "Past Challenge" : "Daily"} · ${formatPuzzleDate(puzzleDate)}`
    : `Practice (${MODE_LABEL[mode]})`;
  const path = isDaily && isPastDate ? paths.pastGame(sport, puzzleDate) : paths.game(sport, mode);

  const rows = guesses.map((g) =>
    g.clues
      .map((c) => {
        const isMatch =
          c.attributeKey === "country" && revealedCountry
            ? c.value.toLowerCase() === revealedCountry.toLowerCase()
            : c.isMatch;
        return isMatch ? "🟩" : c.isClose ? "🟨" : "⬜";
      })
      .join("")
  );

  return [`ID the Athlete · ${sport.name}`, `${which} · ${score}`, ...rows, "", `${origin}${path}`].join("\n");
}

type Status = "idle" | "copied" | "manual";

// Shares through the device's share sheet where there is one, else copies.
// If copying isn't allowed either, the text is shown selected, ready to copy.
export default function ShareResult(props: Omit<ShareInput, "origin">) {
  const [status, setStatus] = useState<Status>("idle");
  const textRef = useRef<HTMLTextAreaElement>(null);
  const text = buildShareText({ ...props, origin: window.location.origin });

  useEffect(() => {
    if (status === "manual") textRef.current?.select();
    if (status !== "copied") return;
    const timer = setTimeout(() => setStatus("idle"), 2000);
    return () => clearTimeout(timer);
  }, [status]);

  const handleShare = async () => {
    if (navigator.share) {
      try {
        await navigator.share({ text });
        return;
      } catch (err) {
        // Closing the share sheet is a choice, not a failure: copy nothing.
        if (err instanceof DOMException && err.name === "AbortError") return;
      }
    }
    try {
      await navigator.clipboard.writeText(text);
      setStatus("copied");
    } catch {
      setStatus("manual");
    }
  };

  return (
    <div className="mt-4 flex flex-col items-center gap-2">
      <button onClick={handleShare} className="btn-card px-5 py-2.5 rounded-md font-semibold text-sm">
        {status === "copied" ? "Copied to clipboard!" : "Share result"}
      </button>
      {status === "manual" && (
        <>
          <label htmlFor="share-text" className="text-xs text-[var(--text-secondary)]">
            Couldn't copy automatically. Copy this instead:
          </label>
          <textarea
            id="share-text"
            ref={textRef}
            readOnly
            value={text}
            rows={text.split("\n").length}
            className="card w-72 max-w-full rounded-md p-2 text-xs text-[var(--text-primary)] font-mono resize-none"
          />
        </>
      )}
    </div>
  );
}
