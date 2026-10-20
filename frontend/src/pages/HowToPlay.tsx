import { ReactNode, useEffect, useState } from "react";
import { useLocation } from "react-router";
import { Sport } from "../types";
import { FamilyGroup, groupByFamily } from "../config/sportFamilies";
import ClueLegend from "../components/ClueLegend";
import CluePill from "../components/CluePill";
import Scenery from "../components/scenery/Scenery";

interface Props {
  sports: Sport[]; // at least one
  onBack: () => void;
}

// 00:00 UTC, when a new Daily Challenge starts, in the reader's own time.
function utcMidnightLocalTime(): string {
  const now = new Date();
  const midnight = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  return midnight.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}

// Every rule of the game in one place. The general rules hold for every
// sport and are always shown; each sport's section (anchor = its family,
// e.g. #cricket) says when its clues count as close, and starts closed
// unless the address names it, so readers open only the sport they play. The numbers come from the server (GameRules
// via GET /api/sports); the closeness rules from config/sportFamilies.
export default function HowToPlay({ sports, onBack }: Props) {
  const { hash } = useLocation();
  const families = groupByFamily(sports);
  // Every tour shares the game's numbers (GameRules), so any one will do.
  const { maxGuesses, hintAfterGuess } = sports[0];
  const withBasis = families.filter((g) => g.display.difficultyBasis);
  const [openFamilies, setOpenFamilies] = useState<ReadonlySet<string>>(() => new Set(hash ? [hash.slice(1)] : []));
  const setOpen = (family: string, open: boolean) =>
    setOpenFamilies((prev) => {
      if (prev.has(family) === open) return prev;
      const next = new Set(prev);
      if (open) next.add(family);
      else next.delete(family);
      return next;
    });

  useEffect(() => {
    document.title = "How to play | ID the Athlete";
  }, []);

  // Opened from a tour (e.g. /how-to-play#cricket): open that sport and go to it.
  useEffect(() => {
    if (!hash) return;
    const family = hash.slice(1);
    setOpenFamilies((prev) => (prev.has(family) ? prev : new Set(prev).add(family)));
    document.getElementById(family)?.scrollIntoView();
  }, [hash]);

  return (
    <div className="isolate min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <Scenery kind="page" />
      <button
        onClick={onBack}
        className="self-start text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)] mb-6"
      >
        ← Back
      </button>

      <h1 className="font-heading text-3xl sm:text-4xl lg:text-5xl text-center text-balance tracking-wide text-[var(--text-primary)] mb-8">How to play</h1>

      <div className="w-full max-w-2xl flex flex-col gap-5">
        <Section title="The goal">
          <p>
            Name the mystery player in {maxGuesses} guesses. Pick a tour, type a player's name and choose them from the
            list. Each guess shows how that player compares with the mystery player, clue by clue.
          </p>
        </Section>

        <Section title="Reading the clues">
          <ul className="flex flex-col gap-2">
            <Row pill={<CluePill state="match" value="Right" icon="✓" />}>Exactly right.</Row>
            <Row pill={<CluePill state="close" value="France" icon="≈" />}>Close, but not exact. What counts as close depends on the sport, below.</Row>
            <Row pill={<CluePill state="miss" value="USA" icon="✕" />}>Not close.</Row>
            <Row pill={<CluePill state="close" value="18" icon="▲" />}>
              For numbers, ▲ means the answer's number is higher and ▼ lower. The colour still shows close or not.
            </Row>
          </ul>
          <p className="mt-3">
            Arrows compare the numbers themselves: for <strong>Career-High Rank</strong>, ▲ means the answer's best
            ranking is a bigger number, so lower down the rankings.
          </p>
        </Section>

        {families
          .filter((g) => g.display.clueRules.length > 0)
          .map((g) => (
            <SportSection key={g.family} group={g} open={openFamilies.has(g.family)} onToggle={(open) => setOpen(g.family, open)} />
          ))}

        <Section title="The hint">
          <p>
            After {hintAfterGuess} guesses you're offered one free hint: the mystery player's country (unless you've
            already found it). Say no and a <strong>Hint available</strong> button keeps it on offer for the rest of the
            game.
          </p>
        </Section>

        <Section title="Daily Challenge">
          <ul className="list-disc pl-5 flex flex-col gap-1">
            <li>One puzzle per tour each day, the same for everyone, and you can play it once.</li>
            <li>
              A new puzzle starts at 00:00 UTC ({utcMidnightLocalTime()} your time).
            </li>
            <li>Your game is saved on this device, so you can leave or refresh and carry on.</li>
            <li>
              Your <strong>streak</strong> counts the days in a row you've won a tour's Daily Challenge. A loss or a missed
              day starts it again.
            </li>
            <li>
              <strong>Past Challenges</strong> lets you play any earlier day's puzzle. They don't count towards your
              streak.
            </li>
          </ul>
        </Section>

        <Section title="Practice: Easy, Medium and Hard">
          <ul className="list-disc pl-5 flex flex-col gap-1">
            <li>As many games as you like, each with a random player.</li>
            <li>
              <strong>Easy</strong> picks only the best-known players, <strong>Medium</strong> adds less famous ones, and{" "}
              <strong>Hard</strong> can be anyone.
              {withBasis.length > 0 && (
                <>
                  {" "}
                  How well-known a player is comes from their career:{" "}
                  {withBasis.map((g, i) => (
                    <span key={g.family}>
                      {i > 0 && (i === withBasis.length - 1 ? " and " : ", ")}
                      {g.display.difficultyBasis} in {g.display.label}
                    </span>
                  ))}
                  .
                </>
              )}
            </li>
            <li>A practice game carries on if you refresh or open another page, until you close the tab, start another, or leave it 90 minutes without a guess.</li>
          </ul>
        </Section>

        <Section title="After the game">
          <ul className="list-disc pl-5 flex flex-col gap-1">
            <li>The answer is revealed, sometimes with a fact about the player.</li>
            <li>
              <strong>Share result</strong> copies a spoiler-free grid of your guesses.
            </li>
          </ul>
        </Section>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="card rounded-md p-5 text-left text-sm text-[var(--text-secondary)]">
      <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-3">{title}</h2>
      {children}
    </section>
  );
}

function Row({ pill, children }: { pill: ReactNode; children: ReactNode }) {
  return (
    <li className="flex items-center gap-3">
      {pill}
      <span>{children}</span>
    </li>
  );
}

interface SportSectionProps {
  group: FamilyGroup;
  open: boolean;
  onToggle: (open: boolean) => void;
}

// A native <details>, so it opens by keyboard and reads correctly to screen
// readers (Chrome and Edge's find-in-page also search it while closed).
function SportSection({ group, open, onToggle }: SportSectionProps) {
  return (
    <section id={group.family} className="card rounded-md p-5 text-left text-sm text-[var(--text-secondary)] scroll-mt-4">
      <details open={open} onToggle={(e) => onToggle(e.currentTarget.open)}>
        <summary className="flex items-center justify-between cursor-pointer list-none [&::-webkit-details-marker]:hidden">
          <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide">{group.display.label} clues</h2>
          <span aria-hidden="true" className="text-[var(--text-muted)] text-xs">
            {open ? "▾ Hide" : "▸ Show"}
          </span>
        </summary>
        <div className="mt-3">
          <ClueLegend family={group.family} />
          <h3 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mt-5 mb-2">When a clue is close</h3>
          <table className="w-full border-collapse">
            <tbody>
              {group.display.clueRules.map((r) => (
                <tr key={r.column} className="border-t border-[var(--border)] align-top">
                  <th scope="row" className="py-2 pr-4 w-1/3 text-left font-semibold text-[var(--text-primary)]">
                    {r.column}
                  </th>
                  <td className="py-2">{r.close}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </section>
  );
}
