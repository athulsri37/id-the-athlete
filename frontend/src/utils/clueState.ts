import { ClueResult } from "../types";

export type ClueState = "match" | "close" | "miss";

// Each state's colours in the active theme; .clue-close and .clue-miss are
// in styles/index.css.
export const CLUE_STATE_CLASSES: Record<ClueState, string> = {
  match: "bg-[var(--accent)] text-[var(--on-accent)]",
  close: "clue-close",
  miss: "clue-miss",
};

// How a guess's clue is shown. Once the country hint is revealed, the
// country clue's match comes from the revealed value instead of the
// server-computed isMatch, per the hint feature's spec: in practice they
// always agree (isMatch was computed against the same true country), this
// just makes the dependency on the revealed value explicit.
export function clueState(clue: ClueResult, revealedCountry?: string | null): ClueState {
  const isMatch =
    clue.attributeKey === "country" && revealedCountry
      ? clue.value.toLowerCase() === revealedCountry.toLowerCase()
      : clue.isMatch;
  if (isMatch) return "match";
  return clue.isClose ? "close" : "miss";
}
