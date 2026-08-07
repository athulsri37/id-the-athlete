import { useEffect } from "react";
import { Sport } from "../types";

export interface TourOption extends Sport {
  label: string; // button text -- can be shorter/different from the full Sport name
}

export interface TourSelectConfig {
  accentText: string; // colored word in the title, e.g. "Tennis" or "Cricketer"
  titleSuffix: string; // trailing text after the accent, e.g. " Player" ("" for none)
  tours: TourOption[];
}

export const TENNIS_TOUR_CONFIG: TourSelectConfig = {
  accentText: "Tennis",
  titleSuffix: " Player",
  tours: [
    { slug: "tennis-men", name: "Men's Tennis", label: "Men's" },
    { slug: "tennis-women", name: "Women's Tennis", label: "Women's" },
  ],
};

export const CRICKET_TOUR_CONFIG: TourSelectConfig = {
  accentText: "Cricketer",
  titleSuffix: "",
  tours: [
    { slug: "cricket-men-international", name: "Men's International Cricket", label: "Men's International" },
    { slug: "cricket-women-international", name: "Women's International Cricket", label: "Women's International" },
  ],
};

interface Props extends TourSelectConfig {
  onSelectTour: (sport: Sport) => void;
  onBack: () => void;
}

export default function TourSelect({ accentText, titleSuffix, tours, onSelectTour, onBack }: Props) {
  useEffect(() => {
    document.title = `ID the ${accentText}${titleSuffix} | ID the Athlete`;
  }, [accentText, titleSuffix]);

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
        <span className="text-[var(--accent-alt)]">{accentText}</span>
        {titleSuffix && <span className="text-[var(--text-primary)]">{titleSuffix}</span>}
      </h1>
      <div className="flex flex-col gap-4 w-full max-w-xs mt-10">
        {tours.map((tour) => (
          <button
            key={tour.slug}
            onClick={() => onSelectTour(tour)}
            className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
          >
            {tour.label}
          </button>
        ))}
      </div>
    </div>
  );
}
