export interface PlayerSummary {
  id: number;
  name: string;
}

export interface Sport {
  slug: string;
  name: string; // e.g. "Men's Tennis"
  shortName: string; // e.g. "Men's"; empty means use name
  family: string; // e.g. "tennis"; picks the sport's rules and display (see config/sportFamilies)
}

export interface AttributeDefinition {
  key: string;
  label: string;
}

export interface ClueResult {
  attributeKey: string;
  label: string;
  type: "categorical" | "numeric";
  value: string;
  isMatch: boolean;
  isClose: boolean;
  direction: "up" | "down" | null;
}

export interface GuessResponse {
  guessedPlayerName: string;
  isCorrect: boolean;
  clues: ClueResult[];
  gameOver: boolean;
  answerName: string | null;
  triviaBlurb: string | null;
  // Daily mode only: send back with the next guess or hint request.
  gameToken: string | null;
}

export type Difficulty = "daily" | "easy" | "medium" | "hard";

// Identifies the game a guess or hint request belongs to. The server counts
// guesses itself: through sessionId for practice games, and for daily games
// through gameToken, the token returned by the previous guess (omitted on
// the first one). date is daily-only: a Past Challenge date, omitted for today.
export interface GameRef {
  mode: Difficulty;
  sessionId?: string;
  date?: string;
  gameToken?: string;
}
