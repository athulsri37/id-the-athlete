import { ClueState } from "../utils/clueState";

interface Props {
  state: ClueState;
  direction: "up" | "down" | null;
}

// The mark after a clue's value: ✓ for a match, an arrow towards the answer
// for a number, ≈ for a close category, ✕ otherwise.
export default function ResultIcon({ state, direction }: Props) {
  if (state === "match") return <span className="text-xs">✓</span>;
  // Direction takes priority over closeness for numeric attributes -- the
  // arrow is still the more useful signal, closeness only changes the pill
  // color. Categorical attributes (country, and Cricket's Role/Bowling
  // Style) have no direction, so they get a distinct "approximately" mark
  // instead.
  if (direction) return <span className="text-xs">{direction === "up" ? "▲" : "▼"}</span>;
  if (state === "close") return <span className="text-xs">≈</span>;
  return <span className="text-xs">✕</span>;
}
