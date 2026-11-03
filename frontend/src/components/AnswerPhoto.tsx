import { PlayerPhoto as Photo } from "../types";
import { commonsPageUrl } from "../utils/commons";
import PlayerPhoto from "./PlayerPhoto";

interface Props {
  name: string;
  photo?: Photo | null;
}

// The mystery player's photo on the result screen, credited underneath with
// a link to the file on Wikimedia Commons, where its author and licence are
// given in full.
export default function AnswerPhoto({ name, photo }: Props) {
  return (
    <figure className="mb-3 flex flex-col items-center">
      <PlayerPhoto name={name} photo={photo} size={112} />
      {photo && (
        <figcaption className="mt-1.5 max-w-xs text-[11px] leading-snug text-[var(--text-muted)]">
          <a
            href={commonsPageUrl(photo.file)}
            target="_blank"
            rel="noopener noreferrer"
            title={`${photo.credit}, ${photo.license}`}
            className="line-clamp-2 underline hover:text-[var(--text-primary)]"
          >
            Photo: {photo.credit}, {photo.license}
          </a>
        </figcaption>
      )}
    </figure>
  );
}
