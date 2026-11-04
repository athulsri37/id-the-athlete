import { useState } from "react";
import { PlayerPhoto as Photo } from "../types";
import { commonsThumbUrl } from "../utils/commons";
import { playerInitials } from "../utils/initials";

interface Props {
  name: string;
  photo?: Photo | null;
  size: number; // px, the circle's width and height
  alt?: string; // empty by default: the name is always shown beside it
  onError?: () => void; // the photo didn't load (the initials show instead)
}

// Commons serves thumbnails at a set of standard widths, which it keeps
// ready; these are the smallest that stay sharp at 2-3x pixel density for
// the width the photo is drawn at (its circle times its zoom).
function thumbWidth(drawnWidth: number): number {
  if (drawnWidth <= 40) return 120;
  if (drawnWidth <= 80) return 250;
  return drawnWidth <= 120 ? 330 : 500;
}

// A player's photo cropped to a circle around its centre point, zoomed in on
// that point, with the credit on hover. A player without a photo, or whose
// photo doesn't load, gets their initials instead.
export default function PlayerPhoto({ name, photo, size, alt = "", onError }: Props) {
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

  // Missing on guesses saved before zoom was added: the whole photo.
  const zoom = photo.zoom ?? 100;
  const centre = `${photo.focusX}% ${photo.focusY}%`;
  return (
    <span
      className="flex-none overflow-hidden rounded-full border border-[var(--border)] bg-[var(--row-alt-bg)]"
      style={style}
    >
      <img
        src={commonsThumbUrl(photo.file, thumbWidth((size * zoom) / 100))}
        alt={alt}
        title={`Photo: ${photo.credit}, ${photo.license}`}
        width={size}
        height={size}
        loading="lazy"
        decoding="async"
        onError={() => {
          setFailedFile(photo.file);
          onError?.();
        }}
        className="h-full w-full max-w-none object-cover"
        style={{
          objectPosition: centre,
          transform: zoom === 100 ? undefined : `scale(${zoom / 100})`,
          transformOrigin: centre,
        }}
      />
    </span>
  );
}
