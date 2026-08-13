import { useEffect, useState } from "react";
import { clearStoredAdminKey, getStoredAdminKey, setUnauthorizedHandler } from "./adminClient";
import AdminLogin from "./AdminLogin";
import PlayersTab from "./PlayersTab";
import SettingsTab from "./SettingsTab";

type Tab = "players" | "settings";

export default function ControlRoom() {
  useEffect(() => {
    document.title = "Control Room";
  }, []);

  // Trusts a key already in sessionStorage until a real request actually
  // 401s (see setUnauthorizedHandler below) -- avoids a throwaway
  // "verify on every reload" round-trip for the common case of an already
  // logged-in session.
  const [authenticated, setAuthenticated] = useState(() => getStoredAdminKey() !== null);
  const [tab, setTab] = useState<Tab>("players");

  useEffect(() => {
    setUnauthorizedHandler(() => setAuthenticated(false));
    return () => setUnauthorizedHandler(null);
  }, []);

  const handleLogout = () => {
    clearStoredAdminKey();
    setAuthenticated(false);
  };

  if (!authenticated) {
    return <AdminLogin onSuccess={() => setAuthenticated(true)} />;
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 px-6 py-6 font-sans">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-xl font-semibold">Control Room</h1>
        <button onClick={handleLogout} className="text-sm text-slate-400 underline hover:text-slate-200">
          Log out
        </button>
      </div>

      <div className="flex gap-2 mb-6 border-b border-slate-800">
        <button
          onClick={() => setTab("players")}
          className={`px-4 py-2 text-sm font-semibold border-b-2 ${
            tab === "players" ? "border-slate-100 text-slate-100" : "border-transparent text-slate-500"
          }`}
        >
          Players
        </button>
        <button
          onClick={() => setTab("settings")}
          className={`px-4 py-2 text-sm font-semibold border-b-2 ${
            tab === "settings" ? "border-slate-100 text-slate-100" : "border-transparent text-slate-500"
          }`}
        >
          Settings
        </button>
      </div>

      {tab === "players" ? <PlayersTab /> : <SettingsTab />}
    </div>
  );
}
