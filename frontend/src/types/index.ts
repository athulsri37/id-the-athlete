export interface PlayerSummary {
  id: number;
  name: string;
}

export interface Sport {
  slug: string;
  name: string; // e.g. "Men's Tennis"
  shortName: string; // e.g. "Men's"; empty means use name
  family: string; // e.g. "tennis"; picks the sport's rules and display (see config/sportFamilies)
  urlName: string; // e.g. "men" in /tennis/men (see routes/paths); empty means use slug
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
  // Null when the player has none; missing on guesses saved before photos
  // were added, which show the player's initials like a player without one.
  guessedPlayerPhoto?: PlayerPhoto | null;
  // Game over only: the mystery player's photo.
  answerPhoto?: PlayerPhoto | null;
}

// A player's photo: a file on Wikimedia Commons (see utils/commons), its
// author and licence, the point to keep in view when it's cropped to a
// circle (percent across and down), and how far to zoom in on that point.
export interface PlayerPhoto {
  file: string;
  credit: string;
  license: string;
  focusX: number;
  focusY: number;
  // Percent, 100 (the whole photo) to 300. Missing on guesses saved before
  // it was added, which show the whole photo.
  zoom?: number;
}

// One photo on the Photo credits page.
export interface PhotoCredit {
  sportSlug: string;
  playerName: string;
  file: string;
  credit: string;
  license: string;
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
