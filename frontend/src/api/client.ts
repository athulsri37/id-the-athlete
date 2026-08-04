import axios from "axios";
import { PlayerSummary, GuessResponse, Difficulty } from "../types";

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
  sessionId?: string
): Promise<GuessResponse> {
  const res = await client.post(
    `/sports/${sportSlug}/game/guess`,
    { playerId, mode, sessionId },
    { params: { guessNumber } }
  );
  return res.data;
}

export async function fetchCountryHint(sportSlug: string, mode: Difficulty, sessionId?: string): Promise<string> {
  const res = await client.get(`/sports/${sportSlug}/game/hint/country`, {
    params: { mode, sessionId },
  });
  return res.data.country as string;
}