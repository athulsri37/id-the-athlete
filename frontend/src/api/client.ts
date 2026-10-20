import axios from "axios";
import { PlayerSummary, GuessResponse, Difficulty, AttributeDefinition, GameRef, Sport } from "../types";

// VITE_API_BASE_URL is the backend's origin in a deployed build (e.g.
// https://<api>.onrender.com). Left unset in local dev, requests stay
// relative and Vite's dev proxy forwards /api to the backend. Vite inlines
// this at build time, so changing it requires a rebuild, not a restart.
export const API_ROOT = `${(import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "")}/api`;

const client = axios.create({ baseURL: API_ROOT });

// The app's first two requests wait for the API to wake (up to a minute on
// the hosting's free plan) behind the startup loader. This gives up well
// after that, so a down API ends in the home screen's "couldn't load"
// message instead of a loader that never goes away.
const STARTUP_TIMEOUT_MS = 90_000;

// Every playable tour with its family -- builds the home and tour-select
// screens, so a new tour needs no frontend change.
export async function fetchSports(): Promise<Sport[]> {
  const res = await client.get(`/sports`, { timeout: STARTUP_TIMEOUT_MS });
  return res.data;
}

export async function fetchPlayerPool(sportSlug: string): Promise<PlayerSummary[]> {
  const res = await client.get(`/sports/${sportSlug}/players`);
  return res.data;
}

export async function fetchActiveTheme(): Promise<string> {
  const res = await client.get(`/settings/theme`, { timeout: STARTUP_TIMEOUT_MS });
  return res.data.theme as string;
}

export async function startPracticeGame(sportSlug: string, difficulty: Exclude<Difficulty, "daily">) {
  const res = await client.post(`/sports/${sportSlug}/game/start`, null, {
    params: { difficulty },
  });
  return res.data as { mode: string; sessionId: string; maxGuesses: number };
}

export async function submitGuess(sportSlug: string, playerId: number, game: GameRef): Promise<GuessResponse> {
  const res = await client.post(`/sports/${sportSlug}/game/guess`, { playerId, ...game });
  return res.data;
}

export async function fetchCountryHint(sportSlug: string, game: GameRef): Promise<string> {
  const res = await client.get(`/sports/${sportSlug}/game/hint/country`, { params: game });
  return res.data.country as string;
}

// The finished game's trivia blurb, using the triviaToken from the game-over
// guess. Null when trivia is unavailable.
export async function fetchTrivia(sportSlug: string, triviaToken: string): Promise<string | null> {
  const res = await client.get(`/sports/${sportSlug}/game/trivia`, { params: { triviaToken } });
  return res.data.triviaBlurb as string | null;
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