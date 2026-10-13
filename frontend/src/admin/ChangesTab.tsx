import { useCallback, useEffect, useState } from "react";
import { problemDetail } from "../api/problemDetails";
import { AdminChange, fetchAdminChanges } from "./adminClient";

// Who changed which player or setting through the admin tool, when, and
// from what to what. Newest first; times in the viewer's local time.
export default function ChangesTab() {
  const [changes, setChanges] = useState<AdminChange[] | null>(null);
  const [error, setError] = useState("");

  const load = useCallback(() => {
    setError("");
    fetchAdminChanges()
      .then(setChanges)
      .catch((err) => setError(problemDetail(err, "Couldn't load the recent changes.")));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="max-w-5xl">
      <div className="flex items-center gap-4 mb-4">
        <p className="text-xs text-slate-400">The latest 100 changes made through this tool, newest first.</p>
        <button onClick={load} className="text-sm text-slate-300 underline hover:text-slate-100">
          Refresh
        </button>
      </div>

      {error && <p className="text-red-400 text-sm mb-4">{error}</p>}

      {changes && changes.length === 0 && <p className="text-sm text-slate-500">No changes yet.</p>}

      {changes && changes.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b border-slate-700 text-left text-slate-400 text-xs uppercase tracking-wide">
                <th className="py-2 pr-4">When</th>
                <th className="py-2 pr-4">Admin</th>
                <th className="py-2 pr-4">What</th>
                <th className="py-2 pr-4">Changes</th>
              </tr>
            </thead>
            <tbody>
              {changes.map((c, i) => (
                <tr key={i} className="border-b border-slate-800 align-top">
                  <td className="py-2 pr-4 text-slate-300 whitespace-nowrap">{new Date(c.at).toLocaleString()}</td>
                  <td className="py-2 pr-4 text-slate-200">{c.admin}</td>
                  <td className="py-2 pr-4 text-slate-200">
                    <span className="block">{c.action}</span>
                    <span className="block text-xs text-slate-400">{c.target}</span>
                  </td>
                  <td className="py-2 pr-4 text-slate-200">{c.changes}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
