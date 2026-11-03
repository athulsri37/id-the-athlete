// What stands in for a player without a photo: the first letters of their
// first and last names. A disambiguation in brackets, as in
// "John Smith (New Zealand)", isn't part of the name.
export function playerInitials(name: string): string {
  const words = name
    .replace(/\(.*?\)/g, " ")
    .split(/\s+/)
    .filter((word) => /\p{L}/u.test(word));
  if (words.length === 0) return "?";
  const letter = (word: string) => word.match(/\p{L}/u)![0].toUpperCase();
  return words.length === 1 ? letter(words[0]) : letter(words[0]) + letter(words[words.length - 1]);
}
