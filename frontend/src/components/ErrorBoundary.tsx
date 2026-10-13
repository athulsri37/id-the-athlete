import { Component, ErrorInfo, ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  failed: boolean;
}

// Catches an unexpected error while showing a screen, so the player gets a
// way out instead of a blank page. React only offers this as a class
// component. Reloading or going home starts the app afresh; a daily game in
// progress is saved after every guess, so it resumes.
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Unexpected error while rendering", error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center justify-center px-4 text-center">
        <h1 className="font-heading text-4xl tracking-wide text-[var(--text-primary)] mb-2">Something went wrong</h1>
        <p className="text-[var(--text-secondary)] mb-8">Reloading the page usually fixes it.</p>
        <div className="flex flex-wrap gap-3 justify-center">
          <button onClick={() => window.location.reload()} className="btn-card px-5 py-3 rounded-md font-semibold text-lg">
            Reload
          </button>
          <button onClick={() => window.location.assign("/")} className="btn-card px-5 py-3 rounded-md font-semibold text-lg">
            Go to the home page
          </button>
        </div>
      </div>
    );
  }
}
