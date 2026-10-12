import { useEffect } from "react";
import { Sport } from "../types";
import { FamilyGroup, tourLabel } from "../config/sportFamilies";

interface Props {
  group: FamilyGroup;
  onSelectTour: (sport: Sport) => void;
  onBack: () => void;
}

// The tours of one sport family, e.g. Men's and Women's Tennis. Both the
// title and the buttons come from data: the family's display config and the
// tours the API returned for it.
export default function TourSelect({ group, onSelectTour, onBack }: Props) {
  const { titleAccent, titleSuffix } = group.display;

  useEffect(() => {
    document.title = `ID the ${titleAccent}${titleSuffix} | ID the Athlete`;
  }, [titleAccent, titleSuffix]);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <button
        onClick={onBack}
        className="self-start text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)] mb-6"
      >
        ← Back
      </button>

      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">{titleAccent}</span>
        {titleSuffix && <span className="text-[var(--text-primary)]">{titleSuffix}</span>}
      </h1>
      <div className="flex flex-col gap-4 w-full max-w-xs mt-10">
        {group.tours.map((tour) => (
          <button
            key={tour.slug}
            onClick={() => onSelectTour(tour)}
            className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
          >
            {tourLabel(tour)}
          </button>
        ))}
      </div>
    </div>
  );
}
