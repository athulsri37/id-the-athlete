import { defineConfig } from "@playwright/test";

// Assumes the backend (dotnet run, http://localhost:5080) and frontend
// (npm run dev, http://localhost:5174) dev servers are already running --
// see the "Testing" section of the root README for details. Deliberately
// no `webServer` block here so this suite never spins up its own servers.
export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: true,
  use: {
    baseURL: "http://localhost:5174",
  },
});
