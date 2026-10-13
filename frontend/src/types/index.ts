export interface PlayerSummary {
  id: number;
  name: string;
}

export interface Sport {
  slug: string;
  name: string; // e.g. "Men's Tennis"
  shortName: string; // e.g. "Men's"; empty means use name
  family: string; // e.g. "tennis"; picks the sport's rules and display (see config/sportFamilies)
  // The game's numbers, as the server enforces them (GameRules), so the
  // page never keeps its own copy.
  maxGuesses: number;
  hintAfterGuess: number;
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
  // Missing on guesses saved before it was added; match those by name.
  guessedPlayerId?: number;
  guessedPlayerName: string;
  isCorrect: boolean;
  clues: ClueResult[];
  gameOver: boolean;
  answerName: string | null;
  // Game over only: unlocks the trivia blurb via fetchTrivia, fetched after
  // the result is shown so the final guess never waits on the AI call.
  triviaToken: string | null;
  // Daily mode only: send back with the next guess or hint request.
  gameToken: string | null;
  // Daily mode only (yyyy-MM-dd): the puzzle this game is for. A game
  // still being played after midnight UTC keeps its original puzzle.
  puzzleDate: string | null;
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
