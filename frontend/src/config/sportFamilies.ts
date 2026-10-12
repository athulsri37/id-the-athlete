import { Sport } from "../types";

// Everything the UI shows that depends on a sport's family (Sport.family),
// as opposed to the individual tour. The tours themselves come from the API
// (GET /api/sports), so a new tour of an existing family needs no frontend
// change. A new family (a new sport) adds an entry here alongside its backend
// ISportRules class; until it has one, it still works with plain defaults.

export interface ClueExample {
  pill: string;
  icon: string;
  state: "match" | "close" | "miss";
  description: string;
}

export interface FamilyDisplay {
  label: string; // home-screen button, e.g. "Tennis"
  titleAccent: string; // coloured word in the tour-select title, e.g. "Tennis" or "Cricketer"
  titleSuffix: string; // text after the accent, e.g. " Player" ("" for none)
  winEmoji?: string; // shown before "Nailed it!"
  legendExamples: ClueExample[];
}

// Order the families appear on the home screen; any family not listed comes
// after these, alphabetically.
const FAMILY_ORDER = ["tennis", "cricket"];

const FAMILY_DISPLAY: Record<string, FamilyDisplay> = {
  tennis: {
    label: "Tennis",
    titleAccent: "Tennis",
    titleSuffix: " Player",
    winEmoji: "🎾",
    legendExamples: [
      { pill: "Right", icon: "✓", state: "match", description: "Match" },
      { pill: "18", icon: "▲", state: "close", description: "Close: near the actual number (e.g. 18 when the answer is 20)" },
      { pill: "France", icon: "≈", state: "close", description: "Close: a nearby country, not exact" },
      { pill: "8", icon: "▲", state: "miss", description: "No match: actual value is higher" },
      { pill: "20", icon: "▼", state: "miss", description: "No match: actual value is lower" },
      { pill: "USA", icon: "✕", state: "miss", description: "No match" },
    ],
  },
  // Cricket has its own closeness mechanics (percent+floor numeric
  // thresholds, Role/Bowling-Style tag matching, regional-bloc country
  // grouping) that don't map onto Tennis's examples -- e.g. Grand Slams and
  // land-border adjacency mean nothing in a Cricket game.
  cricket: {
    label: "Cricket",
    titleAccent: "Cricketer",
    titleSuffix: "",
    winEmoji: "🏏",
    legendExamples: [
      { pill: "12", icon: "▲", state: "close", description: "Close: near the actual wickets count (e.g. 12 when the answer is 14)" },
      { pill: "Batter", icon: "≈", state: "close", description: "Close: shares the Batting tag with the actual role (e.g. Wicketkeeper-Batter)" },
      { pill: "Right-arm Pace", icon: "≈", state: "close", description: "Close: same pace/spin type as the actual bowling style (e.g. Left-arm Pace)" },
      { pill: "Australia", icon: "≈", state: "close", description: "Close: same regional bloc as the actual country (e.g. New Zealand) -- not a shared border" },
    ],
  },
};

// Sport-neutral clue examples for a family without its own entry.
const GENERIC_LEGEND: ClueExample[] = [
  { pill: "Value", icon: "✓", state: "match", description: "Match" },
  { pill: "Value", icon: "≈", state: "close", description: "Close: near the actual value, not exact" },
  { pill: "8", icon: "▲", state: "miss", description: "No match: actual value is higher" },
  { pill: "20", icon: "▼", state: "miss", description: "No match: actual value is lower" },
  { pill: "Value", icon: "✕", state: "miss", description: "No match" },
];

export function familyDisplay(family: string): FamilyDisplay {
  const known = FAMILY_DISPLAY[family];
  if (known) return known;

  const label = family ? family[0].toUpperCase() + family.slice(1) : "Sport";
  return { label, titleAccent: label, titleSuffix: " Player", legendExamples: GENERIC_LEGEND };
}

// A tour's button text on its family's tour-select screen.
export function tourLabel(sport: Sport): string {
  return sport.shortName || sport.name;
}

export interface FamilyGroup {
  family: string;
  display: FamilyDisplay;
  tours: Sport[];
}

// Groups the API's tours by family, in home-screen order. Tours keep the
// order the API returned them in.
export function groupByFamily(sports: Sport[]): FamilyGroup[] {
  const families = [...new Set(sports.map((s) => s.family))];
  const rank = (family: string) => {
    const index = FAMILY_ORDER.indexOf(family);
    return index === -1 ? FAMILY_ORDER.length : index;
  };
  families.sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));

  return families.map((family) => ({
    family,
    display: familyDisplay(family),
    tours: sports.filter((s) => s.family === family),
  }));
}
