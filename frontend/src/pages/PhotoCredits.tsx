import { useEffect, useState } from "react";
import { PhotoCredit, Sport } from "../types";
import { fetchPhotoCredits } from "../api/client";
import { commonsPageUrl } from "../utils/commons";
import Scenery from "../components/scenery/Scenery";

interface Props {
  sports: Sport[];
  onBack: () => void;
}

// Who took each player photo and under which licence, as the licences of
// the Wikimedia Commons photos ask, each linking to the original file.
// Grouped by tour, in the tours' usual order.
export default function PhotoCredits({ sports, onBack }: Props) {
  const [credits, setCredits] = useState<PhotoCredit[] | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    document.title = "Photo credits | ID the Athlete";
  }, []);

  useEffect(() => {
    fetchPhotoCredits()
      .then(setCredits)
      .catch(() => setError(true));
  }, []);

  const tours = sports
    .map((sport) => ({ sport, photos: credits?.filter((c) => c.sportSlug === sport.slug) ?? [] }))
    .filter((tour) => tour.photos.length > 0);

  return (
    <div className="isolate min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <Scenery kind="page" />
      <button
        onClick={onBack}
        className="self-start text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)] mb-6"
      >
        ← Back
      </button>

      <h1 className="font-heading text-3xl sm:text-4xl lg:text-5xl text-center text-balance tracking-wide text-[var(--text-primary)] mb-3">
        Photo credits
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-8 max-w-md text-center">
        The player photos come from Wikimedia Commons, used under the licences their authors chose. Select a credit to
        see the original photo.
      </p>

      {error && <p className="text-[var(--accent-alt)] text-sm">Couldn't load the photo credits.</p>}
      {credits === null && !error && <p className="text-[var(--text-muted)] text-sm">Loading…</p>}
      {credits !== null && tours.length === 0 && (
        <p className="text-[var(--text-muted)] text-sm italic">No player photos yet.</p>
      )}

      <div className="w-full max-w-2xl flex flex-col gap-5">
        {tours.map(({ sport, photos }) => (
          <section key={sport.slug} className="card rounded-md p-5 text-left text-sm">
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-2">{sport.name}</h2>
            <ul>
              {photos.map((photo) => (
                <li
                  key={photo.playerName}
                  className="flex flex-col gap-x-4 border-t border-[var(--border)] py-2 sm:flex-row sm:items-baseline"
                >
                  <span className="font-semibold text-[var(--text-primary)] sm:w-1/3 sm:flex-none">{photo.playerName}</span>
                  <a
                    href={commonsPageUrl(photo.file)}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="min-w-0 text-[var(--text-secondary)] underline [overflow-wrap:anywhere] hover:text-[var(--text-primary)]"
                  >
                    {photo.credit}, {photo.license}
                  </a>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
