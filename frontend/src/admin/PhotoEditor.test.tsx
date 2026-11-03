import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PhotoEditor from "./PhotoEditor";
import { removeAdminPlayerPhoto, setAdminPlayerPhoto } from "./adminClient";

vi.mock("./adminClient", () => ({
  setAdminPlayerPhoto: vi.fn(),
  removeAdminPlayerPhoto: vi.fn(),
}));

const saved = { file: "Federer 2015.jpg", credit: "Jane Doe", license: "CC BY-SA 4.0", focusX: 40, focusY: 20 };

function commonsAnswers(body: unknown, ok = true) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({ ok, status: ok ? 200 : 503, json: () => Promise.resolve(body) })
  );
}

describe("the Control Room's photo editor", () => {
  beforeEach(() => {
    vi.mocked(setAdminPlayerPhoto).mockResolvedValue();
    vi.mocked(removeAdminPlayerPhoto).mockResolvedValue();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("shows a saved photo centred where it was set", () => {
    render(<PhotoEditor playerId={7} photo={saved} />);

    const preview = screen.getByAltText("Photo preview");
    expect(preview.getAttribute("src")).toContain("Special:FilePath/Federer_2015.jpg");
    expect(preview.style.objectPosition).toBe("40% 20%");
    expect(screen.getByRole("button", { name: "Remove photo" })).toBeTruthy();
  });

  it("can't be saved without a credit and licence", () => {
    render(<PhotoEditor playerId={7} photo={null} />);

    fireEvent.change(screen.getByLabelText("Commons file name"), { target: { value: "New.jpg" } });

    expect((screen.getByRole("button", { name: "Save photo" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: "Remove photo" })).toBeNull();
  });

  it("fills the credit and licence from Commons, then saves", async () => {
    commonsAnswers({
      query: {
        pages: { "1": { imageinfo: [{ extmetadata: { Artist: { value: "<a>si.robi</a>" }, LicenseShortName: { value: "CC BY 2.0" } } }] } },
      },
    });
    render(<PhotoEditor playerId={7} photo={null} />);

    fireEvent.change(screen.getByLabelText("Commons file name"), { target: { value: " New.jpg " } });
    fireEvent.click(screen.getByRole("button", { name: "Fill credit" }));
    await screen.findByText(/Filled from Wikimedia Commons/);
    expect((screen.getByLabelText("Credit") as HTMLInputElement).value).toBe("si.robi");
    fireEvent.change(screen.getByLabelText(/Centre across/), { target: { value: "0" } });
    fireEvent.click(screen.getByRole("button", { name: "Save photo" }));

    await screen.findByText("Photo saved.");
    expect(setAdminPlayerPhoto).toHaveBeenCalledWith(7, {
      file: "New.jpg",
      credit: "si.robi",
      license: "CC BY 2.0",
      focusX: 0,
      focusY: 50,
    });
    expect(screen.getByRole("button", { name: "Remove photo" })).toBeTruthy();
  });

  it("says so when Commons doesn't know the file, or can't be reached", async () => {
    commonsAnswers({ query: { pages: { "-1": { missing: "" } } } });
    render(<PhotoEditor playerId={7} photo={null} />);
    fireEvent.change(screen.getByLabelText("Commons file name"), { target: { value: "Typo.jpg" } });

    fireEvent.click(screen.getByRole("button", { name: "Fill credit" }));
    await screen.findByText(/has no details for this file/);

    commonsAnswers({}, false);
    fireEvent.click(screen.getByRole("button", { name: "Fill credit" }));
    await screen.findByText(/Couldn't reach Wikimedia Commons/);
  });

  it("says when saving fails, and keeps what was typed", async () => {
    vi.mocked(setAdminPlayerPhoto).mockRejectedValue(new Error("network"));
    render(<PhotoEditor playerId={7} photo={saved} />);

    fireEvent.click(screen.getByRole("button", { name: "Save photo" }));

    await screen.findByText("Saving the photo failed.");
    expect((screen.getByLabelText("Commons file name") as HTMLInputElement).value).toBe("Federer 2015.jpg");
  });

  it("removes the photo and clears the form", async () => {
    render(<PhotoEditor playerId={7} photo={saved} />);

    fireEvent.click(screen.getByRole("button", { name: "Remove photo" }));

    await screen.findByText("Photo removed.");
    expect(removeAdminPlayerPhoto).toHaveBeenCalledWith(7);
    expect((screen.getByLabelText("Commons file name") as HTMLInputElement).value).toBe("");
    expect(screen.queryByAltText("Photo preview")).toBeNull();
  });

  it("says when Commons has no image by that name", async () => {
    render(<PhotoEditor playerId={7} photo={saved} />);

    fireEvent.error(screen.getByAltText("Photo preview"));

    await waitFor(() => expect(screen.getByText("Wikimedia Commons has no image by this name.")).toBeTruthy());
  });
});
