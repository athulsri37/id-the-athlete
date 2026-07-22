import axios from "axios";
import { PlayerSummary, GuessResponse, Difficulty } from "../types";

const client = axios.create({ baseURL: "/api" });
const SPORT = "tennis";

export async function fetchPlayerPool(): Promise<PlayerSummary[]> {
  const res = await client.get(`/sports/${SPORT}/players`);
  return res.data;
}

export async function fetchActiveTheme(): Promise<string> {
  const res = await client.get(`/settings/theme`);
  return res.data.theme as string;
}

export async function startPracticeGame(difficulty: Exclude<Difficulty, "daily">) {
  const res = await client.post(`/sports/${SPORT}/game/start`, null, {
    params: { difficulty },
  });
  return res.data as { mode: string; sessionId: string; maxGuesses: number };
}

export async function submitGuess(
  playerId: number,
  mode: Difficulty,
  guessNumber: number,
  sessionId?: string
): Promise<GuessResponse> {
  const res = await client.post(
    `/sports/${SPORT}/game/guess`,
    { playerId, mode, sessionId },
    { params: { guessNumber } }
  );
  return res.data;
}

export async function fetchCountryHint(mode: Difficulty, sessionId?: string): Promise<string> {
  const res = await client.get(`/sports/${SPORT}/game/hint/country`, {
    params: { mode, sessionId },
  });
  return res.data.country as string;
}
