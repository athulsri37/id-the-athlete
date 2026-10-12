import { useEffect, useRef, useState } from "react";
import { fetchTrivia } from "../api/client";

interface LoadedTrivia {
  token: string;
  blurb: string;
}

// Fetches a finished game's trivia blurb once triviaToken (from the
// game-over guess) is set, so the final guess never waits on the AI call.
// Returns null until it arrives, and if trivia is unavailable or the
// request fails: it's cosmetic, so there's nothing to show or retry.
//
// onLoaded runs for every blurb that arrives, even after the component has
// moved on to another game or unmounted, so a caller can save it for later.
// It's read when the request starts, so it saves to the game it was for.
export function useGameTrivia(
  sportSlug: string,
  triviaToken: string | null,
  onLoaded?: (blurb: string) => void
): string | null {
  const [loaded, setLoaded] = useState<LoadedTrivia | null>(null);
  const onLoadedRef = useRef(onLoaded);

  // Declared before the fetch effect, so it runs first in the same commit.
  useEffect(() => {
    onLoadedRef.current = onLoaded;
  });

  useEffect(() => {
    if (!triviaToken) return;
    let current = true;
    const onLoadedForThisGame = onLoadedRef.current;
    fetchTrivia(sportSlug, triviaToken)
      .then((blurb) => {
        if (!blurb) return;
        onLoadedForThisGame?.(blurb);
        if (current) setLoaded({ token: triviaToken, blurb });
      })
      .catch(() => {});
    return () => {
      current = false;
    };
  }, [sportSlug, triviaToken]);

  // Keyed by token, so a blurb never outlives the game it belongs to.
  return loaded !== null && loaded.token === triviaToken ? loaded.blurb : null;
}
