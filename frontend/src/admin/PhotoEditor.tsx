import { useState } from "react";
import { problemDetail } from "../api/problemDetails";
import { commonsPageUrl, fetchCommonsCredit } from "../utils/commons";
import PlayerPhoto from "../components/PlayerPhoto";
import { AdminPlayerPhoto, removeAdminPlayerPhoto, setAdminPlayerPhoto } from "./adminClient";

type Status = { kind: "idle" } | { kind: "busy"; what: string } | { kind: "done"; message: string } | { kind: "error"; message: string };

interface Props {
  playerId: number;
  photo: AdminPlayerPhoto | null;
}

const inputClass = "w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm";
const labelClass = "block text-xs uppercase tracking-wide text-slate-400 mb-1";

// The game's photo sizes: the result screen, the desktop table, the phone cards.
const PREVIEWS = [
  { size: 112, label: "Result" },
  { size: 56, label: "Table" },
  { size: 40, label: "Phone" },
];

// The player's photo, a Wikimedia Commons file: pick it by file name, fill
// its credit and licence from Commons, then zoom and move the centre until
// the face fills the circles the game shows, previewed beside the sliders. Saved on its own, apart
// from the player's stats. The parent keys it by player, so switching
// players starts afresh.
export default function PhotoEditor({ playerId, photo }: Props) {
  const [saved, setSaved] = useState(photo !== null);
  const [file, setFile] = useState(photo?.file ?? "");
  const [credit, setCredit] = useState(photo?.credit ?? "");
  const [license, setLicense] = useState(photo?.license ?? "");
  const [focusX, setFocusX] = useState(photo?.focusX ?? 50);
  const [focusY, setFocusY] = useState(photo?.focusY ?? 50);
  const [zoom, setZoom] = useState(photo?.zoom ?? 100);
  const [brokenFile, setBrokenFile] = useState<string | null>(null);
  const [status, setStatus] = useState<Status>({ kind: "idle" });

  const trimmedFile = file.trim();
  const imageBroken = brokenFile === trimmedFile;
  const busy = status.kind === "busy";
  const canSave = trimmedFile !== "" && credit.trim() !== "" && license.trim() !== "" && !busy;

  const fillFromCommons = async () => {
    setStatus({ kind: "busy", what: "Looking up…" });
    try {
      const found = await fetchCommonsCredit(trimmedFile);
      if (!found) {
        setStatus({ kind: "error", message: "Wikimedia Commons has no details for this file. Check the name." });
        return;
      }
      setCredit(found.credit);
      setLicense(found.license);
      setStatus({ kind: "done", message: "Filled from Wikimedia Commons. Check them, then save." });
    } catch {
      setStatus({ kind: "error", message: "Couldn't reach Wikimedia Commons. Try again, or type them yourself." });
    }
  };

  const save = async () => {
    setStatus({ kind: "busy", what: "Saving…" });
    try {
      await setAdminPlayerPhoto(playerId, { file: trimmedFile, credit, license, focusX, focusY, zoom });
      setSaved(true);
      setStatus({ kind: "done", message: "Photo saved." });
    } catch (err) {
      setStatus({ kind: "error", message: problemDetail(err, "Saving the photo failed.") });
    }
  };

  const remove = async () => {
    setStatus({ kind: "busy", what: "Removing…" });
    try {
      await removeAdminPlayerPhoto(playerId);
      setSaved(false);
      setFile("");
      setCredit("");
      setLicense("");
      setFocusX(50);
      setFocusY(50);
      setZoom(100);
      setStatus({ kind: "done", message: "Photo removed." });
    } catch (err) {
      setStatus({ kind: "error", message: problemDetail(err, "Removing the photo failed.") });
    }
  };

  const preview = { file: trimmedFile, credit, license, focusX, focusY, zoom };

  return (
    <section className="mt-5 pt-4 border-t border-slate-700" aria-labelledby="photo-heading">
      <h3 id="photo-heading" className="text-sm font-semibold text-slate-200 mb-3">
        Photo
      </h3>

      <div className="flex flex-col gap-3">
        <div>
          <label htmlFor="photo-file" className={labelClass}>
            Commons file name
          </label>
          <div className="flex gap-2">
            <input
              id="photo-file"
              type="text"
              value={file}
              onChange={(e) => setFile(e.target.value)}
              placeholder="e.g. Roger Federer 2015.jpg"
              className={inputClass}
            />
            <button
              type="button"
              onClick={fillFromCommons}
              disabled={trimmedFile === "" || busy}
              className="flex-shrink-0 rounded-md border border-slate-500 px-3 py-2 text-xs disabled:opacity-50"
            >
              Fill credit
            </button>
          </div>
        </div>

        <div>
          <label htmlFor="photo-credit" className={labelClass}>
            Credit
          </label>
          <input
            id="photo-credit"
            type="text"
            value={credit}
            onChange={(e) => setCredit(e.target.value)}
            maxLength={300}
            className={inputClass}
          />
        </div>

        <div>
          <label htmlFor="photo-license" className={labelClass}>
            Licence
          </label>
          <input
            id="photo-license"
            type="text"
            value={license}
            onChange={(e) => setLicense(e.target.value)}
            maxLength={100}
            placeholder="e.g. CC BY-SA 4.0"
            className={inputClass}
          />
        </div>

        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <div className="flex-none">
            {trimmedFile !== "" && !imageBroken ? (
              <div className="flex items-end gap-4" data-testid="photo-preview">
                {PREVIEWS.map(({ size, label }) => (
                  <div key={size} className="flex flex-col items-center gap-1">
                    <PlayerPhoto
                      name=""
                      photo={preview}
                      size={size}
                      alt={size === PREVIEWS[0].size ? "Photo preview" : ""}
                      onError={() => setBrokenFile(trimmedFile)}
                    />
                    <span className="text-[10px] uppercase tracking-wide text-slate-500">{label}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex h-[112px] w-[112px] items-center justify-center rounded-full border border-dashed border-slate-600 text-center text-xs text-slate-500">
                {imageBroken ? "No image" : "No photo"}
              </div>
            )}
            {trimmedFile !== "" && !imageBroken && (
              <a
                href={commonsPageUrl(trimmedFile)}
                target="_blank"
                rel="noreferrer"
                className="mt-2 inline-block text-xs text-sky-400 underline"
              >
                Open on Wikimedia Commons
              </a>
            )}
            {imageBroken && <p className="text-amber-400 text-xs mt-2">Wikimedia Commons has no image by this name.</p>}
          </div>

          <div className="flex min-w-0 flex-1 flex-col gap-3">
            <div>
              <label htmlFor="photo-zoom" className={labelClass}>
                Zoom: {(zoom / 100).toFixed(2).replace(/\.?0+$/, "")}×
              </label>
              <input
                id="photo-zoom"
                type="range"
                min={100}
                max={300}
                step={5}
                value={zoom}
                onChange={(e) => setZoom(Number(e.target.value))}
                className="w-full"
              />
            </div>
            <div>
              <label htmlFor="photo-focus-x" className={labelClass}>
                Centre across: {focusX}%
              </label>
              <input
                id="photo-focus-x"
                type="range"
                min={0}
                max={100}
                value={focusX}
                onChange={(e) => setFocusX(Number(e.target.value))}
                className="w-full"
              />
            </div>
            <div>
              <label htmlFor="photo-focus-y" className={labelClass}>
                Centre down: {focusY}%
              </label>
              <input
                id="photo-focus-y"
                type="range"
                min={0}
                max={100}
                value={focusY}
                onChange={(e) => setFocusY(Number(e.target.value))}
                className="w-full"
              />
            </div>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3 mt-4">
        <button
          type="button"
          onClick={save}
          disabled={!canSave}
          className="rounded-md bg-slate-100 text-slate-900 font-semibold px-4 py-2 text-sm disabled:opacity-50"
        >
          Save photo
        </button>
        {saved && (
          <button
            type="button"
            onClick={remove}
            disabled={busy}
            className="rounded-md border border-red-400 text-red-300 px-4 py-2 text-sm disabled:opacity-50"
          >
            Remove photo
          </button>
        )}
        <span role="status" className="text-sm">
          {status.kind === "busy" && <span className="text-slate-400">{status.what}</span>}
          {status.kind === "done" && <span className="text-green-400">{status.message}</span>}
          {status.kind === "error" && <span className="text-red-400">{status.message}</span>}
        </span>
      </div>
    </section>
  );
}
