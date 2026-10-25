import { ClueExample } from "../config/sportFamilies";
import { CLUE_STATE_CLASSES } from "../utils/clueState";

interface Props {
  state: ClueExample["state"];
  value: string;
  icon: string;
}

// A sample clue, styled like one in the grid (see ClueGrid): the value,
// then its icon, in the active theme's match/close/miss colours.
export default function CluePill({ state, value, icon }: Props) {
  return (
    <span
      className={`inline-flex items-center gap-1 px-3 py-1 rounded-full text-sm font-semibold flex-shrink-0 whitespace-nowrap ${CLUE_STATE_CLASSES[state]}`}
    >
      {value} {icon}
    </span>
  );
}
