import axios from "axios";

// sessionStorage (not localStorage) so the key survives a reload within
// the tab -- reasonable for "session duration" -- but doesn't linger
// indefinitely on a shared machine once the tab closes.
const ADMIN_KEY_STORAGE_KEY = "controlRoomAdminKey";

let unauthorizedHandler: (() => void) | null = null;

// ControlRoom registers this on mount so a 401 from any admin call
// (including ones from deep inside PlayersTab/SettingsTab) can bounce the
// whole screen back to the login form, not just fail silently.
export function setUnauthorizedHandler(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

export function getStoredAdminKey(): string | null {
  return sessionStorage.getItem(ADMIN_KEY_STORAGE_KEY);
}

export function clearStoredAdminKey() {
  sessionStorage.removeItem(ADMIN_KEY_STORAGE_KEY);
}

const adminClient = axios.create({ baseURL: "/api/admin" });

adminClient.interceptors.request.use((config) => {
  const key = getStoredAdminKey();
  if (key) {
    config.headers.set("X-Admin-Key", key);
  }
  return config;
});

adminClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401) {
      clearStoredAdminKey();
      unauthorizedHandler?.();
    }
    return Promise.reject(error);
  }
);

export interface AdminSport {
  slug: string;
  name: string;
}

export interface AdminPlayerSummary {
  id: number;
  name: string;
}

export interface AdminAttributeValue {
  key: string;
  label: string;
  type: "categorical" | "numeric";
  value: string;
}

export type DifficultyOverride = "Easy" | "Medium" | "Hard";

export interface AdminPlayerDetail {
  id: number;
  name: string;
  attributes: AdminAttributeValue[];
  isOverridden: boolean;
  difficultyOverride: DifficultyOverride | null;
}

export interface AdminSetting {
  key: string;
  value: string;
}

export async function fetchAdminSports(): Promise<AdminSport[]> {
  const res = await adminClient.get("/sports");
  return res.data;
}

export async function fetchAdminPlayers(sportSlug: string): Promise<AdminPlayerSummary[]> {
  const res = await adminClient.get(`/sports/${sportSlug}/players`);
  return res.data;
}

export async function fetchAdminDistinctValues(sportSlug: string, attributeKey: string): Promise<string[]> {
  const res = await adminClient.get(`/sports/${sportSlug}/attributes/${attributeKey}/distinct-values`);
  return res.data;
}

export async function fetchAdminPlayer(playerId: number): Promise<AdminPlayerDetail> {
  const res = await adminClient.get(`/players/${playerId}`);
  return res.data;
}

export async function updateAdminPlayer(
  playerId: number,
  attributes: Record<string, string>,
  isOverridden: boolean,
  difficultyOverride: DifficultyOverride | null
): Promise<void> {
  await adminClient.put(`/players/${playerId}`, { attributes, isOverridden, difficultyOverride });
}

export async function fetchAdminSettings(): Promise<AdminSetting[]> {
  const res = await adminClient.get("/settings");
  return res.data;
}

export async function updateAdminSetting(key: string, value: string): Promise<void> {
  await adminClient.put(`/settings/${encodeURIComponent(key)}`, { value });
}

// There's no dedicated "verify a key" endpoint -- /sports is the cheapest
// real admin call, so the login screen uses it as the verification probe.
// Stores the key first so the interceptor picks it up on this same
// request, then rolls back if the server rejects it.
export async function verifyAndStoreAdminKey(key: string): Promise<boolean> {
  sessionStorage.setItem(ADMIN_KEY_STORAGE_KEY, key);
  try {
    await adminClient.get("/sports");
    return true;
  } catch {
    clearStoredAdminKey();
    return false;
  }
}
