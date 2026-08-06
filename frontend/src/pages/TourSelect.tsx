import { useEffect } from "react";
import { Sport } from "../types";

interface Props {
  onSelectTour: (sport: Sport) => void;
  onBack: () => void;
}

const TOURS: Sport[] = [
  { slug: "tennis-men", name: "Men's Tennis" },
  { slug: "tennis-women", name: "Women's Tennis" },
];

export default function TourSelect({ onSelectTour, onBack }: Props) {
  useEffect(() => {
    document.title = "ID the Tennis Player | ID the Athlete";
  }, []);

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
        <span className="text-[var(--accent-alt)]">Tennis</span>
        <span className="text-[var(--text-primary)]"> Player</span>
      </h1>
      <div className="flex flex-col gap-4 w-full max-w-xs mt-10">
        {TOURS.map((tour) => (
          <button
            key={tour.slug}
            onClick={() => onSelectTour(tour)}
            className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
          >
            {tour.slug === "tennis-men" ? "Men's" : "Women's"}
          </button>
        ))}
      </div>
    </div>
  );
}