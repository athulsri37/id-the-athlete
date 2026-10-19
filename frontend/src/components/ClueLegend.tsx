import { familyDisplay } from "../config/sportFamilies";
import CluePill from "./CluePill";

interface Props {
  family: string;
}

// Each sport family has its own examples, since what counts as "close"
// differs between sports (see config/sportFamilies). Each pill looks exactly
// like a clue in the grid: the value, then its icon (see ClueGrid).
export default function ClueLegend({ family }: Props) {
  const examples = familyDisplay(family).legendExamples;

  return (
    <ul className="flex flex-col gap-2">
      {examples.map((ex) => (
        <li key={ex.description} className="flex items-center gap-3">
          <CluePill state={ex.state} value={ex.pill} icon={ex.icon} />
          <span className="text-[var(--text-secondary)] text-sm">{ex.description}</span>
        </li>
      ))}
    </ul>
  );
}
