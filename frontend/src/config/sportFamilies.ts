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

// One clue column and when it counts as close, for the How to Play page.
// The rules themselves live in the backend (TennisRules, CricketRules and
// the Cricket closeness settings); these describe them and must change with
// them.
export interface ClueRule {
  column: string; // the column's label, as AttributeDefinitions has it
  close: string;
}

export interface FamilyDisplay {
  label: string; // home-screen button, e.g. "Tennis"
  titleAccent: string; // coloured word in the tour-select title, e.g. "Tennis" or "Cricketer"
  titleSuffix: string; // text after the accent, e.g. " Player" ("" for none)
  winEmoji?: string; // shown before "Nailed it!"
  legendExamples: ClueExample[];
  clueRules: ClueRule[]; // empty: How to Play shows only the general rules
  difficultyBasis?: string; // what makes a player Easy or Hard, e.g. "Grand Slams, titles and reaching No. 1"
}

// Order the families appear on the home screen; any family not listed comes
// after these, alphabetically.
const FAMILY_ORDER = ["tennis", "cricket"];

const EXACT_ONLY = "Only an exact match counts";

const FAMILY_DISPLAY: Record<string, FamilyDisplay> = {
  tennis: {
    label: "Tennis",
    titleAccent: "Tennis",
    titleSuffix: " Player",
    winEmoji: "🎾",
    legendExamples: [
      { pill: "Right", icon: "✓", state: "match", description: "Match" },
      { pill: "France", icon: "≈", state: "close", description: "Close: a neighbouring country (e.g. Spain)" },
      { pill: "USA", icon: "✕", state: "miss", description: "No match" },
      { pill: "18", icon: "▲", state: "close", description: "Close, and the answer is higher (e.g. 20 titles)" },
      { pill: "8", icon: "▲", state: "miss", description: "Not close, and the answer is higher" },
      { pill: "20", icon: "▼", state: "miss", description: "Not close, and the answer is lower" },
    ],
    clueRules: [
      { column: "Status, Plays, Backhand", close: EXACT_ONLY },
      { column: "Country", close: "Shares a land border with the answer's country (Australia and New Zealand count too)" },
      { column: "Grand Slams", close: "Within 2" },
      { column: "Career-High Rank", close: "Within 5 places" },
      { column: "Turned Pro", close: "Within 3 years" },
      { column: "Career Titles", close: "Within 5" },
    ],
    difficultyBasis: "reaching No. 1, Grand Slams and career titles",
  },
  // Cricket has its own closeness mechanics (percent+floor numeric
  // thresholds, Role/Bowling-Style tag matching, regional-bloc country
  // grouping) that don't map onto Tennis's examples: Grand Slams and
  // land-border adjacency mean nothing in a Cricket game.
  cricket: {
    label: "Cricket",
    titleAccent: "Cricketer",
    titleSuffix: "",
    winEmoji: "🏏",
    legendExamples: [
      { pill: "Right", icon: "✓", state: "match", description: "Match" },
      { pill: "Australia", icon: "≈", state: "close", description: "Close: same cricket region (e.g. New Zealand)" },
      { pill: "Batter", icon: "≈", state: "close", description: "Close: a related role (e.g. Wicketkeeper-Batter)" },
      { pill: "Right-arm Pace", icon: "≈", state: "close", description: "Close: same type, pace or spin (e.g. Left-arm Pace)" },
      { pill: "India", icon: "✕", state: "miss", description: "No match" },
      { pill: "12", icon: "▲", state: "close", description: "Close, and the answer is higher (e.g. 14 wickets)" },
      { pill: "2010", icon: "▼", state: "miss", description: "Not close, and the answer is lower" },
    ],
    clueRules: [
      { column: "Status, Batting Hand", close: EXACT_ONLY },
      {
        column: "Country",
        close:
          "Same cricket region: Asia (India, Pakistan, Bangladesh, Sri Lanka, Afghanistan, Nepal, UAE), " +
          "Oceania (Australia, New Zealand), British Isles (England, Ireland, Scotland, Netherlands), " +
          "Africa (South Africa, Namibia, Zimbabwe), Americas (West Indies, USA, Canada)",
      },
      { column: "Bowling Style", close: "Same type: both pace or both spin" },
      {
        column: "Role",
        close: "Shares a skill: Batter, Wicketkeeper-Batter and Batting All-rounder all bat; Bowler and Bowling All-rounder both bowl; any two all-rounders",
      },
      { column: "Matches", close: "Within 15% of the answer, or 20 matches if that's more" },
      { column: "Runs", close: "Within 15% of the answer, or 500 runs if that's more" },
      { column: "Wickets", close: "Within 15% of the answer, or 15 wickets if that's more" },
      { column: "Debut Year", close: `${EXACT_ONLY}; the arrow still shows earlier or later` },
    ],
    difficultyBasis: "career matches, runs and wickets",
  },
};

// Sport-neutral clue examples for a family without its own entry.
const GENERIC_LEGEND: ClueExample[] = [
  { pill: "Value", icon: "✓", state: "match", description: "Match" },
  { pill: "Value", icon: "≈", state: "close", description: "Close, not exact" },
  { pill: "8", icon: "▲", state: "miss", description: "Not close, and the answer is higher" },
  { pill: "20", icon: "▼", state: "miss", description: "Not close, and the answer is lower" },
  { pill: "Value", icon: "✕", state: "miss", description: "No match" },
];

export function familyDisplay(family: string): FamilyDisplay {
  const known = FAMILY_DISPLAY[family];
  if (known) return known;

  const label = family ? family[0].toUpperCase() + family.slice(1) : "Sport";
  return { label, titleAccent: label, titleSuffix: " Player", legendExamples: GENERIC_LEGEND, clueRules: [] };
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
