import { useState } from "react";
import { GuessResponse, Difficulty } from "../types";

interface Props {
  guesses: GuessResponse[];
  won: boolean;
  mode: Difficulty;
  sportName: string;
}

function buildShareText(guesses: GuessResponse[], won: boolean, mode: Difficulty, sportName: string): string {
  const modeLabel = mode === "daily" ? "Daily" : mode[0].toUpperCase() + mode.slice(1);
  const lines = guesses.map((g) =>
    g.clues.map((c) => (c.isMatch ? "🟩" : "⬜")).join("")
  );
  const header = `ID the ${sportName} Player: ${modeLabel} ${won ? guesses.length + "/8" : "X/8"}`;
  return [header, ...lines].join("\n");
}

export default function ShareResult({ guesses, won, mode, sportName }: Props) {
  const [copied, setCopied] = useState(false);

  const handleShare = async () => {
    const text = buildShareText(guesses, won, mode, sportName);

    if (navigator.share) {
      try {
        await navigator.share({ text });
        return;
      } catch {
        // user cancelled the share sheet — fall through to clipboard
      }
    }

    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button
      onClick={handleShare}
      className="btn-card mt-4 px-5 py-2.5 rounded-md font-semibold text-sm"
    >
      {copied ? "Copied to clipboard!" : "Share result"}
    </button>
  );
}