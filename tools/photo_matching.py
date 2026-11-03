"""Matching players to Wikidata people for the photo review
(photo_review.py). Pure functions only, so they're tested without the
network (test_photo_matching.py).

A player matches a Wikidata person when the name is the same (accents, dots
and hyphens aside), the person has the tour's occupation and sex, and they
were 12-45 at the player's career start (turned pro or debut), which rules
out a namesake from another era. Country is preferred, not required:
Wikidata's countries are often wrong for cricketers (a West Indian listed
as Australian for a franchise), so a match on everything but country is
shown with a flag for a closer look.
"""

import re
import string
import unicodedata
from dataclasses import dataclass
from html.parser import HTMLParser
from urllib.parse import quote

# Wikidata items and properties used here.
TENNIS_PLAYER = "Q10833314"
CRICKETER = "Q12299841"
MALE = "Q6581097"
FEMALE = "Q6581072"
OCCUPATION, SEX, BIRTH, CITIZENSHIP, COUNTRY_FOR_SPORT, IMAGE = "P106", "P21", "P569", "P27", "P1532", "P18"

# Per tour: who to look for. Every tour the game has must be here.
PHOTO_PROFILES = {
    "tennis-men": {"occupation": TENNIS_PLAYER, "sex": MALE},
    "tennis-women": {"occupation": TENNIS_PLAYER, "sex": FEMALE},
    "cricket-men-international": {"occupation": CRICKETER, "sex": MALE},
    "cricket-women-international": {"occupation": CRICKETER, "sex": FEMALE},
}

# The game's country names -> the names Wikidata may use for the same
# country (lower case). A name not listed only matches itself.
COUNTRY_NAMES = {
    "usa": {"united states", "united states of america"},
    "united kingdom": {"united kingdom", "great britain", "england", "scotland", "wales"},
    "england": {"united kingdom", "england"},
    "west indies": {
        "west indies", "jamaica", "trinidad and tobago", "barbados", "guyana", "antigua and barbuda",
        "saint lucia", "grenada", "saint vincent and the grenadines", "dominica", "saint kitts and nevis",
    },
    "russia": {"russia", "soviet union", "russian empire"},
    "czech republic": {"czech republic", "czechia", "czechoslovakia"},
    "netherlands": {"netherlands", "kingdom of the netherlands"},
    "china": {"china", "people's republic of china"},
    "denmark": {"denmark", "kingdom of denmark"},
    "germany": {"germany", "west germany", "east germany"},
    "serbia": {"serbia", "serbia and montenegro", "yugoslavia", "federal republic of yugoslavia"},
}

MIN_START_AGE, MAX_START_AGE = 12, 45

# How a match is reported.
PHOTO = "photo"
NO_PHOTO = "no photo"
AMBIGUOUS = "more than one match"
NOT_FOUND = "not found"


def strip_disambiguation(name: str) -> str:
    """'John Smith (New Zealand)' -> 'John Smith' (see CLAUDE.md)."""
    return re.sub(r"\s*\([^)]*\)\s*$", "", name)


def normalize_name(name: str) -> str:
    name = unicodedata.normalize("NFKD", strip_disambiguation(name))
    name = "".join(c for c in name if not unicodedata.combining(c))
    name = name.lower().replace("-", " ").replace(".", " ").replace("'", "").replace("’", "")
    return re.sub(r"\s+", " ", name).strip()


def country_matches(ours: str, theirs: set[str]) -> bool:
    """True when Wikidata lists the player's country (or says nothing)."""
    if not ours or not theirs:
        return True
    return bool(COUNTRY_NAMES.get(ours.lower(), {ours.lower()}) & theirs)


@dataclass(frozen=True)
class Person:
    """What the match needs from one Wikidata item."""

    qid: str
    names: frozenset[str]  # normalised label and aliases, every language given
    occupations: frozenset[str]
    sexes: frozenset[str]
    country_ids: frozenset[str]
    birth_year: int | None
    image: str | None  # a Commons file name


def _claim_values(entity: dict, prop: str) -> list:
    values = []
    for claim in entity.get("claims", {}).get(prop, []):
        value = claim.get("mainsnak", {}).get("datavalue", {}).get("value")
        if isinstance(value, dict) and "id" in value:
            values.append(value["id"])
        elif isinstance(value, dict) and "time" in value:
            values.append(int(value["time"][1:5]))
        elif value is not None:
            values.append(value)
    return values


def person_from_entity(entity: dict) -> Person:
    """A Person from a wbgetentities item (props claims|labels|aliases).
    Labels come from every language asked for: famous people's names are
    often only under 'mul' (multilingual), not 'en'."""
    names = {normalize_name(label["value"]) for label in entity.get("labels", {}).values()}
    names |= {normalize_name(alias["value"]) for aliases in entity.get("aliases", {}).values() for alias in aliases}
    births = _claim_values(entity, BIRTH)
    images = _claim_values(entity, IMAGE)
    return Person(
        qid=entity["id"],
        names=frozenset(n for n in names if n),
        occupations=frozenset(_claim_values(entity, OCCUPATION)),
        sexes=frozenset(_claim_values(entity, SEX)),
        country_ids=frozenset(_claim_values(entity, CITIZENSHIP) + _claim_values(entity, COUNTRY_FOR_SPORT)),
        birth_year=births[0] if births else None,
        image=images[0] if images else None,
    )


@dataclass(frozen=True)
class Match:
    status: str  # PHOTO, NO_PHOTO, AMBIGUOUS or NOT_FOUND
    person: Person | None = None
    flags: tuple[str, ...] = ()


def match_player(
    name: str,
    country: str,
    start_year: int | None,
    profile: dict,
    people: list[Person],
    country_names: dict[str, str],
) -> Match:
    """Picks the Wikidata person who is this player, from search results.
    country_names maps a country item to its lower-case English name."""
    wanted = normalize_name(name)
    fits = []
    for person in people:
        if wanted not in person.names or profile["occupation"] not in person.occupations:
            continue
        if person.sexes and profile["sex"] not in person.sexes:
            continue
        if person.birth_year and start_year and not MIN_START_AGE <= start_year - person.birth_year <= MAX_START_AGE:
            continue
        fits.append(person)
    fits = list({p.qid: p for p in fits}.values())

    same_country = [
        p for p in fits if country_matches(country, {country_names.get(c, "") for c in p.country_ids} - {""})
    ]
    flags: tuple[str, ...] = ()
    if same_country:
        fits = same_country
    elif len(fits) == 1:
        listed = sorted({country_names.get(c, "") for c in fits[0].country_ids} - {""})
        flags = (f"Wikidata's country is {', '.join(string.capwords(c) for c in listed)}, not {country}",)

    if not fits:
        return Match(NOT_FOUND)
    if len(fits) > 1:
        return Match(AMBIGUOUS)
    person = fits[0]
    return Match(PHOTO if person.image else NO_PHOTO, person, flags)


class _TextOnly(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.parts: list[str] = []

    def handle_data(self, data: str) -> None:
        self.parts.append(data)


# The server's limits (PlayerPhotoRules).
MAX_CREDIT_LENGTH = 300
MAX_LICENSE_LENGTH = 100


def plain_text(html: str, limit: int) -> str:
    """Commons gives the author as HTML (usually a link); a credit is text."""
    parser = _TextOnly()
    parser.feed(html)
    text = re.sub(r"\s+", " ", "".join(parser.parts)).strip()
    return text if len(text) <= limit else text[: limit - 1] + "…"


@dataclass(frozen=True)
class Credit:
    credit: str
    license: str


def credits_from_imageinfo(response: dict) -> dict[str, Credit]:
    """File name -> credit and licence, from a Commons imageinfo query
    (iiprop=extmetadata) for several titles. Files that are missing, or list
    no author or no licence, are left out: the game can't show them."""
    found: dict[str, Credit] = {}
    for page in response.get("query", {}).get("pages", {}).values():
        if "missing" in page or not page.get("imageinfo"):
            continue
        metadata = page["imageinfo"][0].get("extmetadata", {})
        credit = plain_text(metadata.get("Artist", {}).get("value", ""), MAX_CREDIT_LENGTH)
        license_name = plain_text(metadata.get("LicenseShortName", {}).get("value", ""), MAX_LICENSE_LENGTH)
        if credit and license_name:
            found[page["title"].removeprefix("File:")] = Credit(credit, license_name)
    return found


def commons_thumb_url(file: str, width: int) -> str:
    return f"https://commons.wikimedia.org/wiki/Special:FilePath/{quote(file.replace(' ', '_'))}?width={width}"


def commons_page_url(file: str) -> str:
    return f"https://commons.wikimedia.org/wiki/File:{quote(file.replace(' ', '_'))}"
