import { useState } from "react";
import { PlayerPhoto as Photo } from "../types";
import { commonsThumbUrl } from "../utils/commons";
import { playerInitials } from "../utils/initials";

interface Props {
  name: string;
  photo?: Photo | null;
  size: number; // px, the circle's width and height
}

// Commons serves thumbnails at a set of standard widths, which it keeps
// ready; these are the smallest that stay sharp at 2-3x pixel density.
function thumbWidth(size: number): number {
  return size <= 40 ? 120 : 330;
}

// A player's photo cropped to a circle around its centre point, with the
// credit on hover. A player without a photo, or whose photo doesn't load,
// gets their initials instead. Decorative: the name is always shown beside it.
export default function PlayerPhoto({ name, photo, size }: Props) {
  // The file that failed to load, so another player's photo still tries.
  const [failedFile, setFailedFile] = useState<string | null>(null);
  const style = { width: size, height: size };

  if (!photo || photo.file === failedFile) {
    return (
      <span
        aria-hidden="true"
        data-testid="player-initials"
        className="flex flex-none select-none items-center justify-center rounded-full border border-[var(--border)] bg-[var(--row-alt-bg)] font-bold text-[var(--text-secondary)]"
        style={{ ...style, fontSize: Math.round(size * 0.38) }}
      >
        {playerInitials(name)}
      </span>
    );
  }

  return (
    <img
      src={commonsThumbUrl(photo.file, thumbWidth(size))}
      alt=""
      title={`Photo: ${photo.credit}, ${photo.license}`}
      width={size}
      height={size}
      loading="lazy"
      decoding="async"
      onError={() => setFailedFile(photo.file)}
      className="flex-none max-w-none rounded-full border border-[var(--border)] bg-[var(--row-alt-bg)] object-cover"
      style={{ ...style, objectPosition: `${photo.focusX}% ${photo.focusY}%` }}
    />
  );
}
