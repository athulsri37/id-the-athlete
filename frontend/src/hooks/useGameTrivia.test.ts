import { afterEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useGameTrivia } from "./useGameTrivia";
import { fetchTrivia } from "../api/client";

vi.mock("../api/client", () => ({ fetchTrivia: vi.fn() }));
const mockFetchTrivia = vi.mocked(fetchTrivia);

afterEach(() => {
  vi.resetAllMocks();
});

describe("useGameTrivia", () => {
  it("fetches nothing until the game is over", () => {
    const { result } = renderHook(() => useGameTrivia("tennis-men", null));

    expect(result.current).toBeNull();
    expect(mockFetchTrivia).not.toHaveBeenCalled();
  });

  it("returns the blurb and passes it to onLoaded", async () => {
    mockFetchTrivia.mockResolvedValue("A fun fact.");
    const onLoaded = vi.fn();

    const { result } = renderHook(() => useGameTrivia("tennis-men", "token-1", onLoaded));

    await waitFor(() => expect(result.current).toBe("A fun fact."));
    expect(mockFetchTrivia).toHaveBeenCalledWith("tennis-men", "token-1");
    expect(onLoaded).toHaveBeenCalledWith("A fun fact.");
  });

  it("stays null when trivia is unavailable or the request fails", async () => {
    mockFetchTrivia.mockResolvedValueOnce(null).mockRejectedValueOnce(new Error("network"));
    const onLoaded = vi.fn();

    const { result, rerender } = renderHook(({ token }) => useGameTrivia("tennis-men", token, onLoaded), {
      initialProps: { token: "token-1" },
    });
    rerender({ token: "token-2" });

    await waitFor(() => expect(mockFetchTrivia).toHaveBeenCalledTimes(2));
    expect(result.current).toBeNull();
    expect(onLoaded).not.toHaveBeenCalled();
  });

  it("drops the previous game's blurb when a new game starts", async () => {
    mockFetchTrivia.mockResolvedValue("A fun fact.");

    const { result, rerender } = renderHook(({ token }) => useGameTrivia("tennis-men", token), {
      initialProps: { token: "token-1" as string | null },
    });
    await waitFor(() => expect(result.current).toBe("A fun fact."));

    rerender({ token: null });

    expect(result.current).toBeNull();
  });

  it("still passes a late blurb to the onLoaded it started with after unmounting", async () => {
    let resolve!: (blurb: string) => void;
    mockFetchTrivia.mockReturnValue(new Promise((r) => (resolve = r)));
    const onLoaded = vi.fn();

    const { unmount } = renderHook(() => useGameTrivia("tennis-men", "token-1", onLoaded));
    unmount();
    resolve("A late fact.");

    await waitFor(() => expect(onLoaded).toHaveBeenCalledWith("A late fact."));
  });
});
