import { useState } from "react";
import { GuessResponse, Difficulty } from "../types";

interface Props {
  guesses: GuessResponse[];
  won: boolean;
  mode: Difficulty;
}

function buildShareText(guesses: GuessResponse[], won: boolean, mode: Difficulty): string {
  const modeLabel = mode === "daily" ? "Daily" : mode[0].toUpperCase() + mode.slice(1);
  const lines = guesses.map((g) =>
    g.clues.map((c) => (c.isMatch ? "🟩" : "⬜")).join("")
  );
  const header = `ID the Tennis Player — ${modeLabel} ${won ? guesses.length + "/8" : "X/8"}`;
  return [header, ...lines].join("\n");
}

export default function ShareResult({ guesses, won, mode }: Props) {
  const [copied, setCopied] = useState(false);

  const handleShare = async () => {
    const text = buildShareText(guesses, won, mode);

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
      className="mt-4 px-5 py-2.5 rounded-md bg-ace-500 hover:bg-ace-600 text-court-green font-semibold text-sm transition-colors"
    >
      {copied ? "Copied to clipboard!" : "Share result"}
    </button>
  );
}
