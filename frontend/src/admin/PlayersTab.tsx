import { useEffect, useMemo, useState } from "react";
import {
  AdminPlayerDetail,
  AdminPlayerSummary,
  AdminSport,
  DifficultyOverride,
  fetchAdminDistinctValues,
  fetchAdminPlayer,
  fetchAdminPlayers,
  fetchAdminSports,
  updateAdminPlayer,
} from "./adminClient";

type SaveStatus = "idle" | "saving" | "success" | "error";

const DIFFICULTY_OVERRIDE_OPTIONS: DifficultyOverride[] = ["Easy", "Medium", "Hard"];

export default function PlayersTab() {
  const [sports, setSports] = useState<AdminSport[]>([]);
  const [selectedSport, setSelectedSport] = useState("");
  const [players, setPlayers] = useState<AdminPlayerSummary[]>([]);
  const [search, setSearch] = useState("");
  const [selectedPlayerId, setSelectedPlayerId] = useState<number | null>(null);

  const [playerDetail, setPlayerDetail] = useState<AdminPlayerDetail | null>(null);
  const [formValues, setFormValues] = useState<Record<string, string>>({});
  const [distinctValuesByKey, setDistinctValuesByKey] = useState<Record<string, string[]>>({});
  const [loadingPlayer, setLoadingPlayer] = useState(false);

  const [overrideEnabled, setOverrideEnabled] = useState(false);
  const [overrideValue, setOverrideValue] = useState<DifficultyOverride>("Easy");

  const [numericErrors, setNumericErrors] = useState<Record<string, string>>({});
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("idle");
  const [saveError, setSaveError] = useState("");

  useEffect(() => {
    fetchAdminSports().then((s) => {
      setSports(s);
      if (s.length > 0) setSelectedSport(s[0].slug);
    });
  }, []);

  useEffect(() => {
    if (!selectedSport) return;
    setPlayers([]);
    setSelectedPlayerId(null);
    setPlayerDetail(null);
    fetchAdminPlayers(selectedSport).then(setPlayers);
  }, [selectedSport]);

  useEffect(() => {
    if (selectedPlayerId === null) return;

    let cancelled = false;
    setLoadingPlayer(true);
    setSaveStatus("idle");
    setNumericErrors({});

    fetchAdminPlayer(selectedPlayerId).then(async (detail) => {
      if (cancelled) return;
      setPlayerDetail(detail);
      setFormValues(Object.fromEntries(detail.attributes.map((a) => [a.key, a.value])));
      setOverrideEnabled(detail.isOverridden);
      setOverrideValue(detail.difficultyOverride ?? "Easy");

      const categorical = detail.attributes.filter((a) => a.type === "categorical");
      const entries = await Promise.all(
        categorical.map(async (a) => [a.key, await fetchAdminDistinctValues(selectedSport, a.key)] as const)
      );
      if (cancelled) return;
      setDistinctValuesByKey(Object.fromEntries(entries));
      setLoadingPlayer(false);
    });

    return () => {
      cancelled = true;
    };
  }, [selectedPlayerId, selectedSport]);

  const filteredPlayers = useMemo(() => {
    if (!search.trim()) return players;
    const q = search.trim().toLowerCase();
    return players.filter((p) => p.name.toLowerCase().includes(q));
  }, [players, search]);

  const handleNumericChange = (key: string, value: string) => {
    setFormValues((prev) => ({ ...prev, [key]: value }));
    setNumericErrors((prev) => {
      const next = { ...prev };
      if (value.trim() === "" || Number.isNaN(Number(value))) {
        next[key] = "Must be a number.";
      } else {
        delete next[key];
      }
      return next;
    });
  };

  const handleSave = async () => {
    if (!playerDetail) return;
    if (Object.keys(numericErrors).length > 0) return;

    setSaveStatus("saving");
    setSaveError("");
    try {
      await updateAdminPlayer(playerDetail.id, formValues, overrideEnabled, overrideEnabled ? overrideValue : null);
      setSaveStatus("success");
    } catch (err) {
      setSaveStatus("error");
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "Save failed.";
      setSaveError(message);
    }
  };

  return (
    <div className="flex gap-6">
      <div className="w-72 flex-shrink-0">
        <label className="block text-xs uppercase tracking-wide text-slate-400 mb-1">Sport</label>
        <select
          value={selectedSport}
          onChange={(e) => setSelectedSport(e.target.value)}
          className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm mb-4"
        >
          {sports.map((s) => (
            <option key={s.slug} value={s.slug}>
              {s.name}
            </option>
          ))}
        </select>

        <label className="block text-xs uppercase tracking-wide text-slate-400 mb-1">Search players</label>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Type a name…"
          className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm mb-2"
        />

        <div className="border border-slate-700 rounded-md max-h-[60vh] overflow-y-auto">
          {filteredPlayers.map((p) => (
            <button
              key={p.id}
              onClick={() => setSelectedPlayerId(p.id)}
              className={`w-full text-left px-3 py-2 text-sm border-b border-slate-800 last:border-b-0 hover:bg-slate-800 ${
                selectedPlayerId === p.id ? "bg-slate-800 text-white" : "text-slate-300"
              }`}
            >
              {p.name}
            </button>
          ))}
          {filteredPlayers.length === 0 && (
            <p className="px-3 py-2 text-sm text-slate-500 italic">No players match.</p>
          )}
        </div>
      </div>

      <div className="flex-1">
        {selectedPlayerId === null && <p className="text-slate-500">Select a player to edit.</p>}

        {selectedPlayerId !== null && loadingPlayer && <p className="text-slate-500">Loading…</p>}

        {playerDetail && !loadingPlayer && (
          <div className="max-w-lg">
            <h2 className="text-lg font-semibold mb-4">{playerDetail.name}</h2>

            <div className="flex flex-col gap-3">
              {playerDetail.attributes.map((attr) => (
                <div key={attr.key}>
                  <label className="block text-xs uppercase tracking-wide text-slate-400 mb-1">{attr.label}</label>
                  {attr.type === "numeric" ? (
                    <>
                      <input
                        type="number"
                        value={formValues[attr.key] ?? ""}
                        onChange={(e) => handleNumericChange(attr.key, e.target.value)}
                        className={`w-full rounded-md border bg-slate-800 px-3 py-2 text-sm ${
                          numericErrors[attr.key] ? "border-red-500" : "border-slate-600"
                        }`}
                      />
                      {numericErrors[attr.key] && (
                        <p className="text-red-400 text-xs mt-1">{numericErrors[attr.key]}</p>
                      )}
                    </>
                  ) : (
                    <select
                      value={formValues[attr.key] ?? ""}
                      onChange={(e) => setFormValues((prev) => ({ ...prev, [attr.key]: e.target.value }))}
                      className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm"
                    >
                      {(distinctValuesByKey[attr.key] ?? [attr.value]).map((v) => (
                        <option key={v} value={v}>
                          {v}
                        </option>
                      ))}
                    </select>
                  )}
                </div>
              ))}
            </div>

            <div className="mt-5 pt-4 border-t border-slate-700">
              <label className="flex items-center gap-2 text-sm text-slate-300 cursor-pointer w-fit">
                <input
                  type="checkbox"
                  checked={overrideEnabled}
                  onChange={(e) => setOverrideEnabled(e.target.checked)}
                  className="h-4 w-4"
                />
                Override Difficulty
              </label>
              <p className="text-xs text-slate-500 mt-1">
                Ignores the computed difficulty formula and always places this player in the chosen tier.
              </p>

              {overrideEnabled && (
                <select
                  value={overrideValue}
                  onChange={(e) => setOverrideValue(e.target.value as DifficultyOverride)}
                  className="mt-2 w-full max-w-[12rem] rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm"
                >
                  {DIFFICULTY_OVERRIDE_OPTIONS.map((tier) => (
                    <option key={tier} value={tier}>
                      {tier}
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div className="flex items-center gap-3 mt-5">
              <button
                onClick={handleSave}
                disabled={saveStatus === "saving" || Object.keys(numericErrors).length > 0}
                className="rounded-md bg-slate-100 text-slate-900 font-semibold px-4 py-2 text-sm disabled:opacity-50"
              >
                {saveStatus === "saving" ? "Saving…" : "Save"}
              </button>
              {saveStatus === "success" && <span className="text-green-400 text-sm">Saved.</span>}
              {saveStatus === "error" && <span className="text-red-400 text-sm">{saveError}</span>}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
