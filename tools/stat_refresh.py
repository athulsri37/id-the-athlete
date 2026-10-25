"""Checks a researched stat refresh for active players before anything is
written to the database, and plans what applying it would change.

Pure functions with no database or network access, so they can be unit tested
(see test_stat_refresh.py). The refresh is two steps: research proposes new
values for every active player and saves them; apply re-reads each player,
shows the review table and writes only what the reviewer accepts.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field
from datetime import date, timedelta
from typing import Iterable

COUNT = "count"  # a career total: never goes down
BEST_RANK = "best_rank"  # a career-high ranking: 1 is best, never gets worse
STATUS = "status"  # Active or Retired

ACTIVE = "Active"
RETIRED = "Retired"

_WHOLE_NUMBER = re.compile(r"\d+")


@dataclass(frozen=True)
class FieldRule:
    kind: str  # COUNT, BEST_RANK or STATUS
    # A rise bigger than this between two refreshes is shown for a second
    # look. It's still written if accepted: a long gap between runs can
    # legitimately cover it.
    big_jump: int | None = None


# The only columns a refresh may change, by attribute key (keys are unique
# across sports). Everything else a player has (country, hands, debut year,
# turned-pro year) is fixed and never touched.
REFRESH_RULES = {
    "grand_slam_titles": FieldRule(COUNT, big_jump=1),
    "career_titles": FieldRule(COUNT, big_jump=3),
    "career_high_ranking": FieldRule(BEST_RANK),
    "combined_matches": FieldRule(COUNT, big_jump=20),
    "combined_runs": FieldRule(COUNT, big_jump=1000),
    "combined_wickets": FieldRule(COUNT, big_jump=40),
    "active_status": FieldRule(STATUS),
}

# What the research may answer instead of a value when it can't confirm one.
UNSURE = "unsure"


def refreshable_keys(attribute_keys: Iterable[str]) -> list[str]:
    """The sport's columns a refresh researches, in a stable order."""
    keys = set(attribute_keys)
    return [key for key in REFRESH_RULES if key in keys]


@dataclass
class RefreshCheck:
    player_id: int
    name: str
    # Fields whose researched value differs from the stored one: key ->
    # (before, after), with after in the game's spelling.
    changes: dict[str, tuple[str, str]] = field(default_factory=dict)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    # Fields the research couldn't confirm; they keep their stored value.
    unsure: list[str] = field(default_factory=list)

    @property
    def is_valid(self) -> bool:
        return not self.errors


def check_refresh(
    player_id: int, name: str, current: dict[str, str], proposed: dict[str, object], audit: bool = False
) -> RefreshCheck:
    """Compares one player's researched values with their stored ones.

    Any error blocks the whole player: an answer with one impossible value
    (a total going down, a career high getting worse) is not trusted for its
    other values either. A genuine decrease, such as a record correction, is
    for the reviewer to make by hand in the Control Room.

    audit: checking a retired player's stored values rather than following
    an active player's progress. Their totals are final, so any difference
    is a correction to the stored data, and it can go either way: a total
    going down or a career high getting worse is shown for a second look
    instead of blocked, and runs or wickets may be corrected without the
    matches."""
    result = RefreshCheck(player_id=player_id, name=name)
    after: dict[str, str] = {}

    for key in refreshable_keys(current):
        before = current[key].strip()
        raw = proposed.get(key)
        text = "" if raw is None else str(raw).strip()
        if not text or text.casefold() == UNSURE:
            result.unsure.append(key)
            after[key] = before
            continue

        value = _clean(key, raw, text, result)
        if value is None:
            after[key] = before
            continue
        after[key] = value
        if value != before:
            _check_change(key, before, value, result, audit)
            result.changes[key] = (before, value)

    _check_consistency(after, result, audit)
    return result


def _clean(key: str, raw: object, text: str, result: RefreshCheck) -> str | None:
    if REFRESH_RULES[key].kind == STATUS:
        status = next((s for s in (ACTIVE, RETIRED) if s.casefold() == text.casefold()), None)
        if status is None:
            result.errors.append(f"{key} '{text}' must be {ACTIVE} or {RETIRED}")
        return status
    # bool is an int subclass in Python; true/false is never a valid stat.
    if isinstance(raw, bool) or not _WHOLE_NUMBER.fullmatch(text):
        result.errors.append(f"{key} '{text}' is not a whole number")
        return None
    return str(int(text))


def _check_change(key: str, before: str, after: str, result: RefreshCheck, audit: bool) -> None:
    rule = REFRESH_RULES[key]
    if rule.kind == STATUS:
        if before == RETIRED and after == ACTIVE:
            result.errors.append(
                f"{key} went from {RETIRED} to {ACTIVE}: not written; if it's a comeback, "
                "set it in the Control Room"
            )
        elif after == RETIRED:
            result.warnings.append(f"{key} {before} -> {RETIRED}: check the player has retired")
        return

    if not _WHOLE_NUMBER.fullmatch(before):
        result.errors.append(f"{key}: the stored value '{before}' is not a whole number; fix it in the Control Room")
        return
    old, new = int(before), int(after)

    if rule.kind == BEST_RANK:
        if new > old and audit:
            result.warnings.append(f"{key} corrected, {old} -> {new}: check the source")
        elif new > old:
            result.errors.append(
                f"{key} got worse, {old} -> {new}: a career high never does (a current ranking?); not written"
            )
        elif new == 1:
            result.warnings.append(f"{key} {old} -> 1: check the player reached No. 1")
        return

    if new < old and audit:
        result.warnings.append(f"{key} corrected down, {old} -> {new}: check the source")
    elif new < old:
        result.errors.append(
            f"{key} went down, {old} -> {new}: not written; if it's right, change it in the Control Room"
        )
    elif rule.big_jump is not None and new - old > rule.big_jump:
        result.warnings.append(f"{key} rose by {new - old}, {old} -> {new}: check it")


def _check_consistency(after: dict[str, str], result: RefreshCheck, audit: bool) -> None:
    """Rules between fields, on the values the player would end up with.
    Each rule only judges a refresh that changes one of its fields, so an
    unrelated change (a retirement) isn't blocked by older stored data. An
    audit skips the matches rule: correcting runs or wickets doesn't mean
    more matches were played."""
    if not result.is_valid:
        return

    def rose(key: str) -> bool:
        return key in result.changes and int(result.changes[key][1]) > int(result.changes[key][0])

    if not audit and "combined_matches" in after and not rose("combined_matches"):
        for key in ("combined_runs", "combined_wickets"):
            if rose(key):
                result.errors.append(f"{key} rose but combined_matches didn't; not written")

    slams, titles = after.get("grand_slam_titles"), after.get("career_titles")
    touched = "grand_slam_titles" in result.changes or "career_titles" in result.changes
    if touched and slams and titles and _WHOLE_NUMBER.fullmatch(slams) and _WHOLE_NUMBER.fullmatch(titles):
        if int(slams) > int(titles):
            result.errors.append(
                f"grand_slam_titles ({slams}) is more than career_titles ({titles}); not written"
            )


# A same-name player's disambiguation, e.g. " (New Zealand)" or " (b. 1985)",
# which the research shouldn't search for.
_DISAMBIGUATION = re.compile(r"\s*\([^()]*\)\s*$")


def search_name(name: str) -> str:
    """The name to research a player under, without any disambiguation."""
    return _DISAMBIGUATION.sub("", name).strip()


def same_name(stored: str, answered: str) -> bool:
    """True when the research answered for the name it was given, ignoring
    case, accents, punctuation and the disambiguation."""
    return _name_key(search_name(stored)) == _name_key(answered)


def _name_key(name: str) -> str:
    text = "".join(c for c in unicodedata.normalize("NFKD", name) if not unicodedata.combining(c))
    return " ".join(re.sub(r"[^a-z0-9]+", " ", text.casefold()).split())


def match_answers(requested: Iterable[int], answers: list[dict]) -> tuple[dict[int, dict], list[str]]:
    """The research's answers keyed by the player ID they were asked for,
    plus a note for every answer that can't be used: an ID that wasn't
    asked for, an ID given twice, or a requested player with no answer.
    Unused answers are dropped, never matched by name."""
    wanted = set(requested)
    matched: dict[int, dict] = {}
    problems: list[str] = []
    for answer in answers:
        raw = answer.get("id")
        try:
            player_id = int(str(raw).strip())
        except ValueError:
            problems.append(f"an answer has no usable id ({raw!r}); ignored")
            continue
        if player_id not in wanted:
            problems.append(f"an answer is for id {player_id}, which wasn't asked for; ignored")
        elif player_id in matched:
            problems.append(f"id {player_id} was answered twice; the first answer is kept")
        else:
            matched[player_id] = answer
    problems += [f"no answer for id {player_id}" for player_id in sorted(wanted - set(matched))]
    return matched, problems


def held_players(puzzles: Iterable[tuple[date, int]], today: date) -> dict[int, date]:
    """Players whose changes wait for a later apply, each with the first UTC
    date an apply can write them: the answers of today's and yesterday's
    daily puzzles, since a game started just before midnight can still be
    finished after it, and changing an answer's stats mid-game would judge
    later guesses against different values. A puzzle's answer is released
    two days after its date, once it's no longer today's or yesterday's."""
    held: dict[int, date] = {}
    for puzzle_date, player_id in puzzles:
        if puzzle_date in (today, today - timedelta(days=1)):
            release = puzzle_date + timedelta(days=2)
            held[player_id] = max(release, held.get(player_id, release))
    return held


@dataclass
class ApplyPlan:
    # key -> value to write now.
    write: dict[str, str] = field(default_factory=dict)
    # Fields that already hold the researched value (e.g. a second apply).
    already: list[str] = field(default_factory=list)
    # key -> (value at research, value now): edited since the research ran.
    conflicts: dict[str, tuple[str, str]] = field(default_factory=dict)

    @property
    def can_write(self) -> bool:
        """A conflict skips the whole player, so a half-applied refresh can't
        leave related fields (runs and matches) out of step."""
        return bool(self.write) and not self.conflicts


def plan_apply(changes: dict[str, tuple[str, str]], live: dict[str, str]) -> ApplyPlan:
    """What applying one player's researched changes would do, given the
    values the database holds right now."""
    plan = ApplyPlan()
    for key, (before, after) in changes.items():
        now = live.get(key, "").strip()
        if now == after:
            plan.already.append(key)
        elif now == before:
            plan.write[key] = after
        else:
            plan.conflicts[key] = (before, now)
    return plan


def tier_cell(before: str | None, after: str | None, overridden: bool) -> str:
    """The review table's tier column: "Hard -> Medium" when the change moves
    the player, the tier when it doesn't, "overridden" when the tier is set
    by hand and can't move."""
    if overridden:
        return "overridden"
    if not before or not after:
        return "?"
    return before if before == after else f"{before} -> {after}"


def shown_checks(checks: list[RefreshCheck]) -> list[RefreshCheck]:
    """The players the review table lists, in its order (row 1 first): those
    with a change or an error."""
    return [c for c in checks if c.changes or c.errors]


def review_lines(checks: list[RefreshCheck], tiers: dict[int, str], held: dict[int, date]) -> list[str]:
    """The review table plus a note per player with something to look at.
    Lists only players with a change or an error; everyone else is counted
    in the summary line. Status is "ERROR" (not written), "held" (a recent
    daily answer, written by an apply on or after the date shown), "check" (written
    if accepted, but look at the note) or "ok"."""
    shown = shown_checks(checks)
    headers = ["#", "player", "changes", "tier", "status"]
    rows = []
    for i, check in enumerate(shown, start=1):
        changes = "; ".join(f"{key} {before} -> {after}" for key, (before, after) in check.changes.items())
        rows.append([str(i), check.name, changes or "-", tiers.get(check.player_id, "?"), _status(check, held)])

    lines = []
    if rows:
        widths = [max([len(h)] + [len(row[c]) for row in rows]) for c, h in enumerate(headers)]
        header = " | ".join(h.ljust(widths[c]) for c, h in enumerate(headers))
        lines += [header, "-" * len(header)]
        lines += [" | ".join(cell.ljust(widths[c]) for c, cell in enumerate(row)) for row in rows]

    for i, check in enumerate(shown, start=1):
        if check.errors or check.warnings or check.player_id in held:
            lines.append(f"  {i}. {check.name}:")
            lines += [f"       error: {error}" for error in check.errors]
            lines += [f"       check: {warning}" for warning in check.warnings]
            if check.player_id in held and check.is_valid:
                release = held[check.player_id]
                lines.append(
                    f"       held: a daily answer today or yesterday; run apply again on or after "
                    f"{release:%d %b %Y} (UTC)"
                )

    unchanged = sum(1 for c in checks if not c.changes and not c.errors)
    unsure = sum(1 for c in checks if c.unsure)
    lines.append(f"{len(shown)} with changes or errors, {unchanged} unchanged, {unsure} with a value the research wasn't sure of.")
    return lines


def _status(check: RefreshCheck, held: dict[int, date]) -> str:
    if check.errors:
        return "ERROR"
    if check.player_id in held:
        return "held"
    return "check" if check.warnings else "ok"
