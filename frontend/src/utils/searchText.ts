// Letters that are their own letter rather than a plain one with an accent,
// so stripping accents leaves them as they are.
const SEPARATE_LETTERS: Record<string, string> = { đ: "d", ł: "l", ø: "o", æ: "ae", œ: "oe", ß: "ss", ı: "i" };

// A name or query as it's matched in a search, with accents and case
// ignored, so plain typing finds accented names: "swiatek" finds
// "Iga Świątek". Only for matching: names are always shown as written.
export function normalizeForSearch(value: string): string {
  return value
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .replace(/[đłøæœßı]/g, (letter) => SEPARATE_LETTERS[letter])
    .trim();
}

// Whether a name contains the query, ignoring accents and case.
export function nameMatches(name: string, query: string): boolean {
  return normalizeForSearch(name).includes(normalizeForSearch(query));
}
