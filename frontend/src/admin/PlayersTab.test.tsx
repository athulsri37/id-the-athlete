import { fireEvent, render, screen } from "@testing-library/react";
import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PlayersTab from "./PlayersTab";
import {
  fetchAdminDistinctValues,
  fetchAdminPlayer,
  fetchAdminPlayers,
  fetchAdminSports,
  updateAdminPlayer,
} from "./adminClient";

vi.mock("./adminClient", () => ({
  fetchAdminSports: vi.fn(),
  fetchAdminPlayers: vi.fn(),
  fetchAdminPlayer: vi.fn(),
  fetchAdminDistinctValues: vi.fn(),
  updateAdminPlayer: vi.fn(),
}));

vi.mock("./PhotoEditor", () => ({ default: () => null }));

const nastase = {
  id: 7,
  name: "Ilie Nastase",
  attributes: [{ key: "country", label: "Country", type: "categorical" as const, value: "Romania" }],
  isOverridden: false,
  difficultyOverride: null,
  photo: null,
};

function badRequest(detail: string) {
  const headers = new AxiosHeaders();
  return new AxiosError("Bad Request", "ERR_BAD_REQUEST", { headers }, null, {
    status: 400,
    statusText: "Bad Request",
    headers,
    config: { headers },
    data: { status: 400, detail },
  });
}

async function openNastase() {
  render(<PlayersTab />);
  fireEvent.click(await screen.findByRole("button", { name: "Ilie Nastase" }));
  return (await screen.findByLabelText("Name")) as HTMLInputElement;
}

function playerList() {
  return screen.getAllByRole("button").filter((b) => b.textContent !== "Save").map((b) => b.textContent);
}

describe("the Control Room's player editor", () => {
  beforeEach(() => {
    vi.mocked(fetchAdminSports).mockResolvedValue([{ slug: "tennis-men", name: "Men's Tennis" }]);
    vi.mocked(fetchAdminPlayers).mockResolvedValue([
      { id: 7, name: "Ilie Nastase" },
      { id: 8, name: "Ivan Lendl" },
    ]);
    vi.mocked(fetchAdminPlayer).mockResolvedValue(nastase);
    vi.mocked(fetchAdminDistinctValues).mockResolvedValue(["Romania", "Czechia"]);
    vi.mocked(updateAdminPlayer).mockResolvedValue();
  });
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renames a player, trimmed, and shows the new name in the heading and the list", async () => {
    const name = await openNastase();

    fireEvent.change(name, { target: { value: "  Ilie Năstase " } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByText("Saved.");
    expect(updateAdminPlayer).toHaveBeenCalledWith(7, {
      name: "Ilie Năstase",
      attributes: { country: "Romania" },
      isOverridden: false,
      difficultyOverride: null,
    });
    expect(screen.getByRole("heading").textContent).toBe("Ilie Năstase");
    expect(name.value).toBe("Ilie Năstase");
    expect(playerList()).toEqual(["Ilie Năstase", "Ivan Lendl"]);
  });

  it("leaves the name out when it hasn't changed, so the server keeps it", async () => {
    const name = await openNastase();

    fireEvent.change(name, { target: { value: "Ilie Nastase " } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByText("Saved.");
    expect(vi.mocked(updateAdminPlayer).mock.calls[0][1].name).toBeUndefined();
  });

  it("can't be saved with an empty name", async () => {
    const name = await openNastase();

    fireEvent.change(name, { target: { value: "   " } });

    expect(screen.getByText("Name is required.")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("shows why a rename was refused and keeps the old name in the list", async () => {
    vi.mocked(updateAdminPlayer).mockRejectedValue(badRequest('Another player in this tour is already called "Ivan Lendl".'));
    const name = await openNastase();

    fireEvent.change(name, { target: { value: "Ivan Lendl" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByText('Another player in this tour is already called "Ivan Lendl".');
    expect(screen.getByRole("heading").textContent).toBe("Ilie Nastase");
    expect(playerList()).toEqual(["Ilie Nastase", "Ivan Lendl"]);
    // What was typed stays in the field, to be corrected.
    expect(name.value).toBe("Ivan Lendl");
  });
});
