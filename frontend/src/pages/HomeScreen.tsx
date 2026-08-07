import { useEffect } from "react";

interface Props {
  onSelectTennis: () => void;
  onSelectCricket: () => void;
}

export default function HomeScreen({ onSelectTennis, onSelectCricket }: Props) {
  useEffect(() => {
    document.title = "ID the Athlete";
  }, []);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">Athlete</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-10 max-w-md text-center">
        Guess the mystery athlete using clues. New challenge daily, plus
        practice modes.
      </p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        <button
          onClick={onSelectTennis}
          className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
        >
          Tennis
        </button>
        <button
          onClick={onSelectCricket}
          className="btn-card px-5 py-3 rounded-md font-semibold text-lg"
        >
          Cricket
        </button>
      </div>

      <footer className="mt-auto pt-10 flex items-center justify-center gap-3">
        <a
          href="mailto:athulsri@outlook.com?subject=ID%20the%20Athlete%20Feedback"
          className="text-xs text-[var(--text-muted)] underline hover:text-[var(--accent)] transition-colors"
        >
          Tell me what you think
        </a>
        <span className="w-px h-3 bg-[var(--border)]" aria-hidden="true" />
        <a
          href="https://github.com/athulsri37/id-the-athlete/issues"
          target="_blank"
          rel="noopener noreferrer"
          className="text-xs text-[var(--text-muted)] underline hover:text-[var(--accent)] transition-colors"
        >
          Found a bug? Report it on GitHub
        </a>
      </footer>
    </div>
  );
}