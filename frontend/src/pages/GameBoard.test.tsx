import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { AxiosError, AxiosResponse } from "axios";
import GameBoard from "./GameBoard";
import * as client from "../api/client";
import { GuessResponse, Sport } from "../types";

vi.mock("../api/client");

const SPORT: Sport = { slug: "tennis-men", name: "Men's Tennis", shortName: "Men's", family: "tennis", urlName: "men", maxGuesses: 8, hintAfterGuess: 5 };

const PLAYERS = [
  { id: 1, name: "Roger Federer" },
  { id: 2, name: "Rafael Nadal" },
];

function guessOf(name: string, id: number): GuessResponse {
  return {
    guessedPlayerId: id,
    guessedPlayerName: name,
    isCorrect: false,
    clues: [{ attributeKey: "country", label: "Country", type: "categorical", value: "Spain", isMatch: false, isClose: false, direction: null }],
    gameOver: false,
    answerName: null,
    triviaToken: null,
    gameToken: null,
    puzzleDate: null,
  };
}

function renderGame(props: { startFresh?: boolean } = {}) {
  return render(
    <MemoryRouter>
      <GameBoard mode="easy" sport={SPORT} onBackToHome={() => {}} {...props} />
    </MemoryRouter>
  );
}

async function guess(name: string) {
  const input = await screen.findByPlaceholderText("Type a player name");
  await vi.waitFor(() => expect(input).not.toBeDisabled());
  fireEvent.change(input, { target: { value: name.slice(0, 4) } });
  fireEvent.click(await screen.findByRole("button", { name }));
}

let started = 0;

beforeEach(() => {
  started = 0;
  vi.mocked(client.fetchPlayerPool).mockResolvedValue(PLAYERS);
  vi.mocked(client.fetchAttributeDefinitions).mockResolvedValue([{ key: "country", label: "Country" }]);
  vi.mocked(client.startPracticeGame).mockImplementation(async () => ({ sessionId: `session-${++started}` }) as Awaited<ReturnType<typeof client.startPracticeGame>>);
  vi.mocked(client.submitGuess).mockImplementation(async (_slug, playerId) => guessOf(PLAYERS.find((p) => p.id === playerId)!.name, playerId));
});

afterEach(() => {
  sessionStorage.clear();
  vi.clearAllMocks();
});

describe("GameBoard practice games", () => {
  it("carries on with the same game after leaving and coming back", async () => {
    const first = renderGame();
    await guess("Roger Federer");
    // The guess shows in both the wide-screen table and the phone cards (CSS
    // picks one); the card is the one that can be found by its name.
    expect(await screen.findByRole("region", { name: "Guess 1: Roger Federer" })).toBeInTheDocument();
    first.unmount();

    renderGame();

    expect(await screen.findByRole("region", { name: "Guess 1: Roger Federer" })).toBeInTheDocument();
    expect(client.startPracticeGame).toHaveBeenCalledTimes(1);
    await guess("Rafael Nadal");
    expect(vi.mocked(client.submitGuess).mock.calls[vi.mocked(client.submitGuess).mock.calls.length - 1][2].sessionId).toBe("session-1");
  });

  it("starts a new game when the difficulty was chosen from the menu", async () => {
    const first = renderGame();
    await guess("Roger Federer");
    first.unmount();

    renderGame({ startFresh: true });

    await vi.waitFor(() => expect(client.startPracticeGame).toHaveBeenCalledTimes(2));
    expect(screen.queryAllByText("Roger Federer")).toHaveLength(0);
  });

  it("offers a new game when the server no longer has this one", async () => {
    vi.mocked(client.submitGuess).mockRejectedValueOnce(
      new AxiosError("Bad Request", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 400,
        data: { detail: "Practice session not found or has expired. Start a new game." },
      } as AxiosResponse)
    );
    renderGame();
    await guess("Roger Federer");

    expect(await screen.findByText(/has expired/)).toBeInTheDocument();
    expect(screen.getByPlaceholderText("This game has ended")).toBeDisabled();
    expect(sessionStorage.length).toBe(0);

    fireEvent.click(screen.getByRole("button", { name: "Start a new game" }));

    await vi.waitFor(() => expect(client.startPracticeGame).toHaveBeenCalledTimes(2));
    expect(await screen.findByPlaceholderText("Type a player name")).not.toBeDisabled();
    expect(screen.queryByText(/has expired/)).not.toBeInTheDocument();
  });
});
