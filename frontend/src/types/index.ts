export interface PlayerSummary {
  id: number;
  name: string;
}

export interface ClueResult {
  attributeKey: string;
  label: string;
  type: "categorical" | "numeric";
  value: string;
  isMatch: boolean;
  direction: "up" | "down" | null;
}

export interface GuessResponse {
  guessedPlayerName: string;
  isCorrect: boolean;
  clues: ClueResult[];
  gameOver: boolean;
  answerName: string | null;
  triviaBlurb: string | null;
}

export type Difficulty = "daily" | "easy" | "medium" | "hard";
