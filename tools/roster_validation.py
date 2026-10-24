"""Checks a researched player's values against what the game accepts, before
anything is written to the database.

Pure functions with no database or network access, so they can be unit tested
(see test_roster_validation.py). The roster agent loads the AttributeSpecs for
a sport from the database and calls validate_player on every proposal.
"""

from __future__ import annotations

import difflib
import re
from dataclasses import dataclass, field

NUMERIC = "numeric"
CATEGORICAL = "categorical"

# Categorical attributes where a value the game hasn't seen before is
# legitimate (a player from a new country), so it's a warning, not an error.
# Every other categorical attribute has a fixed vocabulary (hands, roles,
# bowling styles, status) that the clue rules match exactly.
OPEN_VALUE_ATTRIBUTES = frozenset({"country"})

_WHOLE_NUMBER = re.compile(r"\d+")

# Other names for countries the game already spells one way, so the same
# country is never stored twice (e.g. "United States" next to "USA"): the
# clues compare names exactly, so a second spelling never matches or counts
# as close. Keys are casefolded; values are the game's spelling, which the
# closeness rules (CountryProximity, CricketRules) also use. Spelling slips
# ("Austrailia") are caught separately by the "did you mean" hint.
COUNTRY_ALIASES = {
    "united states": "USA",
    "united states of america": "USA",
    "us": "USA",
    "u.s.": "USA",
    "u.s.a.": "USA",
    "america": "USA",
    "great britain": "United Kingdom",
    "britain": "United Kingdom",
    "gb": "United Kingdom",
    "gbr": "United Kingdom",
    "uk": "United Kingdom",
    "u.k.": "United Kingdom",
    "czechia": "Czech Republic",
    "russian federation": "Russia",
    "united arab emirates": "UAE",
    "u.a.e.": "UAE",
    "bosnia & herzegovina": "Bosnia and Herzegovina",
    "bosnia-herzegovina": "Bosnia and Herzegovina",
    "the netherlands": "Netherlands",
    "holland": "Netherlands",
    "korea": "South Korea",
    "republic of korea": "South Korea",
    "korea, republic of": "South Korea",
    "republic of ireland": "Ireland",
    "windies": "West Indies",
}

# The game's difficulty tiers, easiest first (see the backend's ISportRules).
TIERS = ("easy", "medium", "hard")


ANY_TIER = "any"


def parse_tiers(text: str) -> frozenset[str] | None:
    """Reads a --tiers value like "medium,hard", or "any" for no preference
    (None). Raises ValueError naming any entry that isn't a tier."""
    if text.strip().lower() == ANY_TIER:
        return None
    tiers = {part.strip().lower() for part in text.split(",") if part.strip()}
    unknown = sorted(tiers - set(TIERS))
    if unknown or not tiers:
        raise ValueError(
            f"tiers must be '{ANY_TIER}' or a comma-separated list of {', '.join(TIERS)}; got '{text}'"
        )
    return frozenset(tiers)


def describe_tiers(tiers: frozenset[str]) -> str:
    """e.g. "medium or hard", always in easy-to-hard order."""
    return " or ".join(t for t in TIERS if t in tiers)


def tier_warning(tier: str | None, wanted: frozenset[str] | None) -> str | None:
    """A note when a player's actual tier isn't one the reviewer asked for.
    None when there's no preference, the tier is unknown, or it matches."""
    if not wanted or tier not in TIERS or tier in wanted:
        return None
    return f"tier is {tier}, but you asked for {describe_tiers(wanted)}"


# Hosts that mean "this PC": its own Docker database. An empty host is a
# local socket connection.
LOCAL_HOSTS = frozenset({"", "localhost", "127.0.0.1", "::1"})


def write_confirmation(host: str | None, dbname: str | None) -> str:
    """What the reviewer must type before players are written: "yes" for a
    database on this PC, the database's own name for any other (production),
    so writing there can't be a reflex."""
    if (host or "").strip().lower() in LOCAL_HOSTS:
        return "yes"
    return dbname or "?"


@dataclass(frozen=True)
class AttributeSpec:
    """One of a sport's clue columns, from its AttributeDefinitions row."""

    key: str
    kind: str  # NUMERIC or CATEGORICAL
    # Values already used for this attribute in the sport. For a fixed
    # vocabulary these are the accepted values.
    known_values: frozenset[str] = frozenset()


@dataclass
class ValidationResult:
    name: str
    # Cleaned values ready to write: whole numbers without leading zeros,
    # categorical values in the game's existing spelling and case.
    values: dict[str, str] = field(default_factory=dict)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def is_valid(self) -> bool:
        return not self.errors


def validate_player(player: dict, specs: list[AttributeSpec]) -> ValidationResult:
    """Errors make a player unsafe to write; warnings are written but shown to
    the reviewer. Every spec must have a value: a missing one is an error."""
    name = _as_text(player.get("name"))
    result = ValidationResult(name=name)
    if not name:
        result.errors.append("name is missing")

    for spec in specs:
        raw = player.get(spec.key)
        text = _as_text(raw)
        if not text:
            result.errors.append(f"{spec.key} is missing")
        elif spec.kind == NUMERIC:
            _check_number(spec, raw, text, result)
        else:
            _check_category(spec, text, result)

    return result


def schema_mismatch(profile_fields: list[str], specs: list[AttributeSpec]) -> list[str]:
    """Differences between the fields the agent researches and the columns the
    sport actually has, as readable problems (empty when they agree)."""
    researched = set(profile_fields) - {"name"}
    columns = {spec.key for spec in specs}
    problems = [f"the database has no '{key}' column for this sport" for key in sorted(researched - columns)]
    problems += [f"the agent doesn't research the '{key}' column" for key in sorted(columns - researched)]
    return problems


def standard_country(text: str) -> str:
    """The game's spelling of a country given under another name, or the
    text unchanged when it isn't a known alias."""
    return COUNTRY_ALIASES.get(text.strip().casefold(), text.strip())


def prompt_countries(specs: list[AttributeSpec]) -> list[str]:
    """The countries a sport already uses, in the game's spelling, for the
    agent's prompt. A value stored under an alias is left out, so the prompt
    never teaches the agent the wrong spelling."""
    country = next((spec for spec in specs if spec.key == "country"), None)
    if country is None:
        return []
    return sorted(v for v in country.known_values if standard_country(v) == v)


def _as_text(value: object) -> str:
    return "" if value is None else str(value).strip()


def _check_number(spec: AttributeSpec, raw: object, text: str, result: ValidationResult) -> None:
    # bool is an int subclass in Python; true/false is never a valid stat.
    if isinstance(raw, bool) or not _WHOLE_NUMBER.fullmatch(text):
        result.errors.append(f"{spec.key} '{text}' is not a whole number")
        return
    result.values[spec.key] = str(int(text))


def _check_category(spec: AttributeSpec, text: str, result: ValidationResult) -> None:
    if spec.key == "country":
        text = standard_country(text)
    canonical = next((v for v in spec.known_values if v.casefold() == text.casefold()), None)
    if canonical is not None:
        result.values[spec.key] = canonical
        return

    suggestion = _closest(text, spec.known_values)
    hint = f" (did you mean '{suggestion}'?)" if suggestion else ""

    # A brand-new sport has no players yet, so there's no vocabulary to check
    # against; accept the value but make sure the reviewer looks at it.
    if spec.key in OPEN_VALUE_ATTRIBUTES or not spec.known_values:
        result.values[spec.key] = text
        result.warnings.append(
            f"{spec.key} '{text}' is new to the game{hint}: check the spelling, and add it to "
            "the sport's closeness rules if it should ever count as close"
        )
        return

    accepted = ", ".join(sorted(spec.known_values))
    result.errors.append(f"{spec.key} '{text}' isn't an accepted value{hint}; accepted: {accepted}")


def _closest(text: str, candidates: frozenset[str]) -> str | None:
    by_folded = {c.casefold(): c for c in candidates}
    matches = difflib.get_close_matches(text.casefold(), list(by_folded), n=1, cutoff=0.75)
    return by_folded[matches[0]] if matches else None
