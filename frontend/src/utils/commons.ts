// Player photos are files on Wikimedia Commons, stored by file name (see
// Player.PhotoFile on the server). These build the addresses to show and
// credit one, and read a file's credit and licence from Commons itself.

const COMMONS = "https://commons.wikimedia.org";

// The server's limit on a stored credit (PlayerPhotoRules.MaxCreditLength).
export const MAX_CREDIT_LENGTH = 300;

function commonsTitle(file: string): string {
  return encodeURIComponent(file.trim().replace(/ /g, "_"));
}

// A resized copy, served by Commons: no copy of the photo is kept anywhere.
export function commonsThumbUrl(file: string, width: number): string {
  return `${COMMONS}/wiki/Special:FilePath/${commonsTitle(file)}?width=${width}`;
}

// The file's own page, where its author and licence are given in full.
export function commonsPageUrl(file: string): string {
  return `${COMMONS}/wiki/File:${commonsTitle(file)}`;
}

export interface CommonsCredit {
  credit: string;
  license: string;
}

interface ExtMetadataValue {
  value?: string;
}

interface ImageInfoResponse {
  query?: {
    pages?: Record<
      string,
      {
        missing?: string;
        imageinfo?: { extmetadata?: Record<string, ExtMetadataValue | undefined> }[];
      }
    >;
  };
}

// Commons gives the author as HTML (usually a link); a credit is plain text.
export function plainText(html: string): string {
  const text = new DOMParser().parseFromString(html, "text/html").body.textContent ?? "";
  const collapsed = text.replace(/\s+/g, " ").trim();
  return collapsed.length > MAX_CREDIT_LENGTH ? `${collapsed.slice(0, MAX_CREDIT_LENGTH - 1)}…` : collapsed;
}

// The credit and licence Commons lists for a file, from its API response.
// Null when the file doesn't exist or lists neither.
export function readCommonsCredit(response: ImageInfoResponse): CommonsCredit | null {
  const page = Object.values(response.query?.pages ?? {})[0];
  if (!page || page.missing !== undefined) return null;
  const metadata = page.imageinfo?.[0]?.extmetadata ?? {};
  const credit = plainText(metadata.Artist?.value ?? "");
  const license = plainText(metadata.LicenseShortName?.value ?? "");
  return credit || license ? { credit, license } : null;
}

// Asks Commons directly from the browser (its API allows any site to read
// it), so looking a file up needs nothing from our server.
export async function fetchCommonsCredit(file: string, signal?: AbortSignal): Promise<CommonsCredit | null> {
  const params = new URLSearchParams({
    action: "query",
    prop: "imageinfo",
    iiprop: "extmetadata",
    iiextmetadatafilter: "Artist|LicenseShortName",
    titles: `File:${file.trim()}`,
    format: "json",
    origin: "*",
  });
  const response = await fetch(`${COMMONS}/w/api.php?${params}`, { signal });
  if (!response.ok) throw new Error(`Commons answered ${response.status}`);
  return readCommonsCredit((await response.json()) as ImageInfoResponse);
}
