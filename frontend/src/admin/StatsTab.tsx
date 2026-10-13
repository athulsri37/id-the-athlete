import { ReactNode, useEffect, useState } from "react";
import { problemDetail } from "../api/problemDetails";
import { addDays, todayUtcDateString } from "../utils/localDate";
import { AdminSport, AdminStats, fetchAdminSports, fetchAdminStats } from "./adminClient";

const DEFAULT_DAYS = 30;

const percent = (value: number | null) => (value === null ? "–" : `${value}%`);

// The README's four "Game Stats" views over the games started in a date
// range (UTC, both ends inclusive), for one tour or all of them. Unfinished
// games count as abandoned after 90 minutes without a guess.
export default function StatsTab() {
  const [sports, setSports] = useState<AdminSport[]>([]);
  const [sport, setSport] = useState("");
  const [to, setTo] = useState(todayUtcDateString);
  const [from, setFrom] = useState(() => addDays(todayUtcDateString(), -(DEFAULT_DAYS - 1)));
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchAdminSports().then(setSports).catch(() => setError("Couldn't load the tours."));
  }, []);

  // Refetches whenever a filter changes; a slower, older response never
  // overwrites a newer one.
  useEffect(() => {
    let current = true;
    setError("");
    fetchAdminStats({ sport, from, to })
      .then((result) => {
        if (current) setStats(result);
      })
      .catch((err) => {
        if (current) setError(problemDetail(err, "Couldn't load the stats."));
      });
    return () => {
      current = false;
    };
  }, [sport, from, to]);

  return (
    <div className="max-w-4xl space-y-8">
      <div className="flex flex-wrap items-end gap-4">
        <label className="text-xs text-slate-400">
          Tour
          <select
            value={sport}
            onChange={(e) => setSport(e.target.value)}
            className="block mt-1 rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm text-slate-100"
          >
            <option value="">All tours</option>
            {sports.map((s) => (
              <option key={s.slug} value={s.slug}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs text-slate-400">
          From (UTC)
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="block mt-1 rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm text-slate-100"
          />
        </label>
        <label className="text-xs text-slate-400">
          To (UTC)
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="block mt-1 rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm text-slate-100"
          />
        </label>
      </div>

      {error && <p className="text-red-400 text-sm">{error}</p>}

      {stats && (
        <>
          <Section
            title="How games end"
            note="A game counts from its first guess. Unfinished games are abandoned after 90 minutes without a guess."
            headers={["Tour", "Mode", "Games", "Won", "Lost", "Abandoned", "In progress", "Finished"]}
            rows={stats.outcomes.map((r) => [r.tour, r.mode, r.games, r.won, r.lost, r.abandoned, r.inProgress, `${r.finishedPercent}%`])}
          />
          <Section
            title="Which guess people win on"
            headers={["Tour", "Won on guess", "Games"]}
            rows={stats.winningGuesses.map((r) => [r.tour, r.guess, r.games])}
          />
          <Section
            title="The hint"
            note="Finished games that reached the hint (offered after 5 guesses)."
            headers={["Tour", "Reached hint", "Took hint", "Win % with hint", "Win % without"]}
            rows={stats.hints.map((r) => [r.tour, r.reachedHint, r.tookHint, percent(r.winPercentWithHint), percent(r.winPercentWithoutHint)])}
          />
          <Section
            title="Daily puzzles"
            note="Includes Past Challenge plays of each puzzle."
            headers={["Puzzle date", "Tour", "Athlete", "Games", "Win %", "Avg winning guess"]}
            rows={stats.puzzles.map((r) => [r.puzzleDate, r.tour, r.athlete, r.games, `${r.winPercent}%`, r.averageWinningGuess ?? "–"])}
          />
        </>
      )}
    </div>
  );
}

interface SectionProps {
  title: string;
  note?: string;
  headers: string[];
  rows: ReactNode[][];
}

function Section({ title, note, headers, rows }: SectionProps) {
  return (
    <section>
      <h2 className="text-sm font-semibold text-slate-100">{title}</h2>
      {note && <p className="text-xs text-slate-400 mt-1">{note}</p>}
      {rows.length === 0 ? (
        <p className="text-sm text-slate-500 mt-3">No games in this range.</p>
      ) : (
        <div className="overflow-x-auto mt-3">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b border-slate-700 text-left text-slate-400 text-xs uppercase tracking-wide">
                {headers.map((h) => (
                  <th key={h} className="py-2 pr-4 font-semibold">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((cells, i) => (
                <tr key={i} className="border-b border-slate-800">
                  {cells.map((cell, j) => (
                    <td key={j} className="py-2 pr-4 text-slate-200">
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
