import { useEffect } from "react";
import { FamilyGroup } from "../config/sportFamilies";
import Scenery from "../components/scenery/Scenery";

const FEEDBACK_EMAIL = "idtheathlete@gmail.com";

interface Props {
  // One button per family, in display order. Null while still loading.
  families: FamilyGroup[] | null;
  loadFailed: boolean;
  onSelectFamily: (family: string) => void;
  onHowToPlay: () => void;
}

export default function HomeScreen({ families, loadFailed, onSelectFamily, onHowToPlay }: Props) {
  useEffect(() => {
    document.title = "ID the Athlete";
  }, []);

  return (
    <div className="isolate min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <Scenery kind="home" />
      <h1 className="font-heading text-3xl sm:text-4xl lg:text-5xl text-center text-balance tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">Athlete</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-10 max-w-md text-center">
        Guess the mystery athlete using clues. New challenge daily, plus
        practice modes.
      </p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        {loadFailed && (
          <p className="text-[var(--accent-alt)] text-sm text-center">
            Couldn't load the sports. Refresh the page to try again.
          </p>
        )}
        {families?.map(({ family, display }) => (
          <button
            key={family}
            onClick={() => onSelectFamily(family)}
            className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
          >
            {display.label}
          </button>
        ))}
      </div>

      <button
        onClick={onHowToPlay}
        className="mt-6 text-sm text-[var(--link-accent)] underline hover:text-[var(--link-accent-hover)]"
      >
        How to play
      </button>

      {/* The address is shown as text too: a mailto link does nothing on a
          computer with no email app set up. */}
      <footer className="mt-auto pt-10 text-xs text-[var(--text-muted)] text-center">
        Feedback or found a bug?{" "}
        <a
          href={`mailto:${FEEDBACK_EMAIL}?subject=ID%20the%20Athlete%20Feedback`}
          className="underline hover:text-[var(--accent)] transition-colors"
        >
          {FEEDBACK_EMAIL}
        </a>
      </footer>
    </div>
  );
}