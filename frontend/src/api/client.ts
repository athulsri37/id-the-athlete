import axios from "axios";
import { PlayerSummary, GuessResponse, Difficulty, AttributeDefinition } from "../types";

const client = axios.create({ baseURL: "/api" });

export async function fetchPlayerPool(sportSlug: string): Promise<PlayerSummary[]> {
  const res = await client.get(`/sports/${sportSlug}/players`);
  return res.data;
}

export async function fetchActiveTheme(): Promise<string> {
  const res = await client.get(`/settings/theme`);
  return res.data.theme as string;
}

export async function startPracticeGame(sportSlug: string, difficulty: Exclude<Difficulty, "daily">) {
  const res = await client.post(`/sports/${sportSlug}/game/start`, null, {
    params: { difficulty },
  });
  return res.data as { mode: string; sessionId: string; maxGuesses: number };
}

export async function submitGuess(
  sportSlug: string,
  playerId: number,
  mode: Difficulty,
  guessNumber: number,
  sessionId?: string,
  date?: string
): Promise<GuessResponse> {
  const res = await client.post(
    `/sports/${sportSlug}/game/guess`,
    { playerId, mode, sessionId, date },
    { params: { guessNumber } }
  );
  return res.data;
}

export async function fetchCountryHint(
  sportSlug: string,
  mode: Difficulty,
  sessionId?: string,
  date?: string
): Promise<string> {
  const res = await client.get(`/sports/${sportSlug}/game/hint/country`, {
    params: { mode, sessionId, date },
  });
  return res.data.country as string;
}

// Every date (yyyy-MM-dd) with an existing Daily Challenge puzzle for this
// sport, most recent first -- powers the Past Challenges list.
export async function fetchDailyPuzzleDates(sportSlug: string): Promise<string[]> {
  const res = await client.get(`/sports/${sportSlug}/daily-puzzles`);
  return res.data;
}

// This sport's attribute set, in display order -- powers the clue-grid
// column headers. Never hardcode a sport's attribute list on the frontend;
// each sport (Tennis, Cricket, ...) defines its own, and this is the only
// source of truth for what they are.
export async function fetchAttributeDefinitions(sportSlug: string): Promise<AttributeDefinition[]> {
  const res = await client.get(`/sports/${sportSlug}/attributes`);
  return res.data;
}