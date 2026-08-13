import { useEffect, useState } from "react";
import { AdminSetting, fetchAdminSettings, updateAdminSetting } from "./adminClient";

type RowStatus = "idle" | "saving" | "success" | "error";

// Input type is inferred purely from each setting's CURRENT value, per
// spec -- there's no schema anywhere declaring a setting's type, so this
// is the only signal available: exactly "true"/"false" -> boolean
// dropdown, otherwise a parseable number -> number input, otherwise text.
function inferKind(value: string): "boolean" | "number" | "text" {
  if (value === "true" || value === "false") return "boolean";
  if (value.trim() !== "" && !Number.isNaN(Number(value))) return "number";
  return "text";
}

export default function SettingsTab() {
  const [settings, setSettings] = useState<AdminSetting[]>([]);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<Record<string, RowStatus>>({});
  const [error, setError] = useState<Record<string, string>>({});

  const load = () => {
    fetchAdminSettings().then((rows) => {
      setSettings(rows);
      setDraft(Object.fromEntries(rows.map((r) => [r.key, r.value])));
    });
  };

  useEffect(() => {
    load();
  }, []);

  const handleSave = async (key: string) => {
    setStatus((prev) => ({ ...prev, [key]: "saving" }));
    setError((prev) => ({ ...prev, [key]: "" }));
    try {
      await updateAdminSetting(key, draft[key]);
      setStatus((prev) => ({ ...prev, [key]: "success" }));
      setSettings((prev) => prev.map((s) => (s.key === key ? { ...s, value: draft[key] } : s)));
    } catch (err) {
      setStatus((prev) => ({ ...prev, [key]: "error" }));
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "Save failed.";
      setError((prev) => ({ ...prev, [key]: message }));
    }
  };

  return (
    <div className="max-w-2xl">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="border-b border-slate-700 text-left text-slate-400 text-xs uppercase tracking-wide">
            <th className="py-2 pr-4">Key</th>
            <th className="py-2 pr-4">Value</th>
            <th className="py-2">Action</th>
          </tr>
        </thead>
        <tbody>
          {settings.map((s) => {
            // Kind is inferred from the ORIGINAL value (settings[].value),
            // not the in-progress draft -- so the input type stays stable
            // while typing, e.g. editing a number's digits doesn't
            // flip-flop the row between number/text mid-keystroke.
            const kind = inferKind(s.value);
            const rowStatus = status[s.key] ?? "idle";

            return (
              <tr key={s.key} className="border-b border-slate-800">
                <td className="py-2 pr-4 font-mono text-xs text-slate-300">{s.key}</td>
                <td className="py-2 pr-4">
                  {kind === "boolean" ? (
                    <select
                      value={draft[s.key] ?? s.value}
                      onChange={(e) => setDraft((prev) => ({ ...prev, [s.key]: e.target.value }))}
                      className="rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm"
                    >
                      <option value="true">True</option>
                      <option value="false">False</option>
                    </select>
                  ) : kind === "number" ? (
                    <input
                      type="number"
                      value={draft[s.key] ?? s.value}
                      onChange={(e) => setDraft((prev) => ({ ...prev, [s.key]: e.target.value }))}
                      className="rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm w-32"
                    />
                  ) : (
                    <input
                      type="text"
                      value={draft[s.key] ?? s.value}
                      onChange={(e) => setDraft((prev) => ({ ...prev, [s.key]: e.target.value }))}
                      className="rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm w-56"
                    />
                  )}
                </td>
                <td className="py-2">
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleSave(s.key)}
                      disabled={rowStatus === "saving" || draft[s.key] === s.value}
                      className="rounded-md bg-slate-100 text-slate-900 font-semibold px-3 py-1.5 text-xs disabled:opacity-50"
                    >
                      {rowStatus === "saving" ? "Saving…" : "Save"}
                    </button>
                    {rowStatus === "success" && <span className="text-green-400 text-xs">Saved</span>}
                    {rowStatus === "error" && <span className="text-red-400 text-xs">{error[s.key]}</span>}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
