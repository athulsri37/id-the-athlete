import { useTheme } from "../../theme/ThemeContext";
import { SceneKind, sceneFor } from "./scenes";

interface Props {
  kind: SceneKind;
  family?: string; // the sport family the screen belongs to, if any
}

// A screen's background artwork, fixed behind its content (see scenes.ts
// for which screen gets what). The screen's root must be `isolate`, so the
// artwork sits above the screen's background colour and below its content.
// Purely decorative: hidden from screen readers and never takes a click.
export default function Scenery({ kind, family }: Props) {
  const scene = sceneFor(useTheme(), kind, family);
  if (!scene) return null;

  const { wide: Wide, narrow: Narrow } = scene;
  return (
    <div aria-hidden="true" className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
      {Wide === Narrow ? (
        <Wide />
      ) : (
        <>
          <div className="absolute inset-0 hidden lg:block">
            <Wide />
          </div>
          <div className="absolute inset-0 lg:hidden">
            <Narrow />
          </div>
        </>
      )}
    </div>
  );
}
