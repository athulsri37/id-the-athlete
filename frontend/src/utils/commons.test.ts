import { describe, expect, it } from "vitest";
import { commonsPageUrl, commonsThumbUrl, MAX_CREDIT_LENGTH, plainText, readCommonsCredit } from "./commons";

describe("Commons addresses", () => {
  it("uses the file name as Commons writes it in an address", () => {
    expect(commonsThumbUrl("Federer WM16 (37).jpg", 120)).toBe(
      "https://commons.wikimedia.org/wiki/Special:FilePath/Federer_WM16_(37).jpg?width=120"
    );
    expect(commonsPageUrl("Li Na & co.jpg")).toBe("https://commons.wikimedia.org/wiki/File:Li_Na_%26_co.jpg");
  });
});

describe("reading a file's credit from Commons", () => {
  const response = (metadata: Record<string, { value: string }>) => ({
    query: { pages: { "50252380": { imageinfo: [{ extmetadata: metadata }] } } },
  });

  it("turns the author's HTML link into plain text and keeps the licence", () => {
    const credit = readCommonsCredit(
      response({
        Artist: { value: '<a rel="nofollow" href="https://www.flickr.com/people/1">si.robi</a>' },
        LicenseShortName: { value: "CC BY-SA 2.0" },
      })
    );
    expect(credit).toEqual({ credit: "si.robi", license: "CC BY-SA 2.0" });
  });

  it("is null for a file Commons doesn't have", () => {
    expect(readCommonsCredit({ query: { pages: { "-1": { missing: "" } } } })).toBeNull();
  });

  it("is null when the file lists neither an author nor a licence", () => {
    expect(readCommonsCredit(response({}))).toBeNull();
  });

  it("shortens a very long credit to what the server accepts", () => {
    const text = plainText(`<p>${"word ".repeat(200)}</p>`);
    expect(text.length).toBe(MAX_CREDIT_LENGTH);
    expect(text.endsWith("…")).toBe(true);
  });
});
