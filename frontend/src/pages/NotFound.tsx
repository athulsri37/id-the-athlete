import { useEffect } from "react";
import { Link } from "react-router";

// Shown for any address that doesn't name a screen: an unknown sport, tour
// or mode, or a Past Challenge date with no puzzle (e.g. an old or mistyped
// link).
export default function NotFound() {
  useEffect(() => {
    document.title = "Page not found | ID the Athlete";
  }, []);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center justify-center px-4 text-center">
      <h1 className="font-heading text-4xl tracking-wide text-[var(--text-primary)] mb-2">Page not found</h1>
      <p className="text-[var(--text-secondary)] mb-8">We couldn't find that page.</p>
      <Link to="/" className="btn-card px-5 py-3 rounded-md font-semibold text-lg">
        Go to the home page
      </Link>
    </div>
  );
}
