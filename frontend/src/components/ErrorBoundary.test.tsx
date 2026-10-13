import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import ErrorBoundary from "./ErrorBoundary";

function Broken(): JSX.Element {
  throw new Error("boom");
}

describe("ErrorBoundary", () => {
  it("shows its children when nothing goes wrong", () => {
    render(
      <ErrorBoundary>
        <p>All good</p>
      </ErrorBoundary>
    );
    expect(screen.getByText("All good")).toBeInTheDocument();
  });

  it("replaces a crashed screen with a way out instead of a blank page", () => {
    // React and the boundary both log the error; keep the test output clean.
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>
    );

    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reload" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Go to the home page" })).toBeInTheDocument();
    consoleError.mockRestore();
  });
});
