"""
Photo review: suggests a photo for each player without one
===========================================================

    python photo_review.py tennis-men [--limit 50]

For production, run scripts/review-photos-prod.sh instead: it asks for the
database URL and admin key, and backs production up first.

For every player of the tour without a photo, it looks the player up on
Wikidata (free, no AI and no subscription use), matches by name, sport,
sex, and age at career start (photo_matching.py), and takes the photo
Wikidata links to, a file on Wikimedia Commons, with the credit and licence
Commons lists for it.

You review them on a page it opens in your browser
(photo_review_<tour>.html, next to this script; local only), where each
suggestion is numbered and shows the photo as the game will crop it,
alongside the player's details and links to the Wikidata and Commons pages.
Back in the terminal you choose what to write, then type the database's
name (or "yes" locally). Each photo is written through the Control Room's
photo endpoint, so it's validated and logged as "Photo review" in the
Changes tab, centred in the middle: move the centre in the Control Room
where the face sits elsewhere. A player who got a photo since the lookup is
skipped. Photos you leave out are suggested again next run.

--limit N looks up at most N players this run (the ones without a photo,
in ID order).

Needs ROSTER_DB_URL (default: the local dev database), ROSTER_API_URL and
ROSTER_ADMIN_KEY: the same variables as the other tools. On a PC where
Python can't check Wikimedia's certificate ("certificate has expired"), set
SSL_CERT_FILE to an up-to-date CA bundle (the production script does this
with Git's).
"""

import argparse
import html
import json
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from contextlib import closing
from pathlib import Path

import psycopg2
import psycopg2.errors

from photo_matching import (
    AMBIGUOUS,
    NO_PHOTO,
    NOT_FOUND,
    PHOTO,
    PHOTO_PROFILES,
    Credit,
    Match,
    Person,
    commons_page_url,
    commons_thumb_url,
    credits_from_imageinfo,
    match_player,
    person_from_entity,
    strip_disambiguation,
)
from roster_research_agent import ADMIN_KEY, API_URL, DB_URL, db_write_confirmation, describe_db_target
from stat_refresh_agent import api_request, check_admin_key, describe_http_error, parse_row_numbers, positive_int

WIKIDATA_API = "https://www.wikidata.org/w/api.php"
COMMONS_API = "https://commons.wikimedia.org/w/api.php"
# Wikimedia asks automated clients to say who they are.
USER_AGENT = "IdTheAthletePhotoReview/1.0 (https://github.com/athulsri37/id-the-athlete)"
# Smaller than the API's 50: famous people's items are large, and a batch of
# several can come back incomplete.
ENTITY_BATCH = 20
SEARCH_RESULTS = 10


# ============================================================
# DATABASE (read only: every write goes through the API)
# ============================================================

def load_players_without_photo(sport_slug: str) -> list[dict]:
    """The tour's players with no photo: id, name, country, career start."""
    query_sql = """
        SELECT p."Id", p."Name",
               max(v."Value") FILTER (WHERE ad."Key" = 'country'),
               max(v."Value") FILTER (WHERE ad."Key" IN ('turned_pro_year', 'debut_year'))
        FROM "Players" p
        JOIN "Sports" s ON s."Id" = p."SportId"
        LEFT JOIN "PlayerAttributeValues" v ON v."PlayerId" = p."Id"
        LEFT JOIN "AttributeDefinitions" ad ON ad."Id" = v."AttributeDefinitionId"
        WHERE s."Slug" = %s AND p."PhotoFile" IS NULL
        GROUP BY p."Id", p."Name"
        ORDER BY p."Id";
    """
    try:
        with closing(psycopg2.connect(DB_URL)) as conn, conn.cursor() as cur:
            cur.execute(query_sql, (sport_slug,))
            rows = cur.fetchall()
    except psycopg2.OperationalError as e:
        sys.exit(f"ERROR: couldn't connect to {describe_db_target()}. If it's the local one, is Docker/Postgres running?\n{e}")
    except psycopg2.errors.UndefinedColumn:
        sys.exit(f"ERROR: {describe_db_target()} has no photo columns yet: start the API once so it applies its migrations.")
    return [
        {"id": pid, "name": name, "country": country or "", "start": int(start) if start and start.isdigit() else None}
        for pid, name, country, start in rows
    ]


# ============================================================
# WIKIMEDIA (read only)
# ============================================================

def wikimedia(url: str, params: dict) -> dict:
    request = urllib.request.Request(
        f"{url}?{urllib.parse.urlencode({**params, 'format': 'json'})}", headers={"User-Agent": USER_AGENT}
    )
    for attempt in range(1, 4):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                # strict=False: a few names on Wikidata hold stray control characters.
                return json.loads(response.read().decode("utf-8"), strict=False)
        except urllib.error.URLError as e:
            if isinstance(e.reason, ssl.SSLCertVerificationError):
                sys.exit(
                    f"ERROR: Python couldn't check Wikimedia's certificate ({e.reason.verify_message}). Set SSL_CERT_FILE "
                    "to an up-to-date CA bundle, e.g. Git's: export SSL_CERT_FILE=\"$(cygpath -w /mingw64/etc/ssl/certs/ca-bundle.crt)\""
                )
            if attempt == 3:
                sys.exit(f"ERROR: Wikimedia didn't answer: {e}")
        except (TimeoutError, json.JSONDecodeError) as e:
            if attempt == 3:
                sys.exit(f"ERROR: Wikimedia didn't answer properly: {e}")
        time.sleep(5 * attempt)
    raise AssertionError("unreachable")


def search_people(name: str, occupation: str) -> list[str]:
    """Wikidata items with this occupation whose text matches the name."""
    result = wikimedia(WIKIDATA_API, {
        "action": "query", "list": "search", "srlimit": SEARCH_RESULTS,
        "srsearch": f"{strip_disambiguation(name)} haswbstatement:P106={occupation}",
    })
    return [hit["title"] for hit in result.get("query", {}).get("search", [])]


def fetch_entities(qids: list[str], props: str) -> dict[str, dict]:
    """Items by ID, in small batches, retrying one by one any a batch left out."""
    found: dict[str, dict] = {}
    for i in range(0, len(qids), ENTITY_BATCH):
        batch = qids[i:i + ENTITY_BATCH]
        result = wikimedia(WIKIDATA_API, {"action": "wbgetentities", "ids": "|".join(batch), "props": props, "languages": "en|mul"})
        found.update({q: e for q, e in result.get("entities", {}).items() if "missing" not in e})
        for qid in batch:
            if qid not in found:
                single = wikimedia(WIKIDATA_API, {"action": "wbgetentities", "ids": qid, "props": props, "languages": "en|mul"})
                found.update({q: e for q, e in single.get("entities", {}).items() if "missing" not in e})
    return found


def country_names(people: list[Person]) -> dict[str, str]:
    ids = sorted({c for p in people for c in p.country_ids})
    entities = fetch_entities(ids, "labels")
    return {
        qid: (e.get("labels", {}).get("en") or e.get("labels", {}).get("mul") or {}).get("value", "").lower()
        for qid, e in entities.items()
    }


def fetch_credits(files: list[str]) -> dict[str, Credit]:
    found: dict[str, Credit] = {}
    for i in range(0, len(files), 50):
        result = wikimedia(COMMONS_API, {
            "action": "query", "prop": "imageinfo", "iiprop": "extmetadata",
            "iiextmetadatafilter": "Artist|LicenseShortName",
            "titles": "|".join(f"File:{f}" for f in files[i:i + 50]),
        })
        # Commons normalises titles (underscores to spaces); map back.
        normalized = {n["to"]: n["from"] for n in result.get("query", {}).get("normalized", [])}
        for title, credit in credits_from_imageinfo(result).items():
            found[normalized.get(f"File:{title}", f"File:{title}").removeprefix("File:")] = credit
    return found


def look_up(players: list[dict], profile: dict) -> list[tuple[dict, Match]]:
    print(f"Looking up {len(players)} players on Wikidata...")
    candidates: dict[int, list[str]] = {}
    for i, player in enumerate(players, start=1):
        candidates[player["id"]] = search_people(player["name"], profile["occupation"])
        if i % 25 == 0 or i == len(players):
            print(f"  {i} of {len(players)}")
        time.sleep(0.1)  # Wikimedia asks clients not to hammer the API

    entities = fetch_entities(sorted({q for qs in candidates.values() for q in qs}), "claims|labels|aliases")
    people = {qid: person_from_entity(e) for qid, e in entities.items()}
    names = country_names(list(people.values()))
    return [
        (player, match_player(player["name"], player["country"], player["start"], profile,
                              [people[q] for q in candidates[player["id"]] if q in people], names))
        for player in players
    ]


# ============================================================
# REVIEW PAGE
# ============================================================

def review_page(sport_slug: str, rows: list[tuple[dict, Match, Credit]]) -> str:
    cards = []
    for number, (player, match, credit) in enumerate(rows, start=1):
        file = match.person.image
        thumb = html.escape(commons_thumb_url(file, 250))
        flags = "".join(f'<p class="flag">{html.escape(flag)}</p>' for flag in match.flags)
        born = f", born {match.person.birth_year}" if match.person.birth_year else ""
        start = f", career from {player['start']}" if player["start"] else ""
        cards.append(f"""
<article>
  <div class="number">{number}</div>
  <div class="pics"><img class="round big" src="{thumb}" alt=""><img class="round small" src="{thumb}" alt=""><img class="full" src="{thumb}" alt=""></div>
  <h2>{html.escape(player['name'])}</h2>
  <p>{html.escape(player['country'])}{html.escape(start)}{html.escape(born)}</p>
  {flags}
  <p class="credit">{html.escape(credit.credit)} · {html.escape(credit.license)}</p>
  <p><a href="https://www.wikidata.org/wiki/{match.person.qid}" target="_blank" rel="noreferrer">Wikidata</a> ·
     <a href="{html.escape(commons_page_url(file))}" target="_blank" rel="noreferrer">Commons</a></p>
</article>""")
    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Photo review: {html.escape(sport_slug)}</title>
<style>
  body {{ font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; margin: 24px; }}
  h1 {{ font-size: 20px; }}
  main {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }}
  article {{ position: relative; background: #1e293b; border-radius: 10px; padding: 14px; }}
  .number {{ position: absolute; top: 10px; left: 10px; background: #f8fafc; color: #0f172a; font-weight: 700;
             border-radius: 999px; min-width: 28px; height: 28px; display: grid; place-items: center; }}
  .pics {{ display: flex; align-items: flex-end; gap: 10px; margin-left: 34px; }}
  .round {{ border-radius: 50%; object-fit: cover; object-position: 50% 50%; background: #334155; }}
  .big {{ width: 96px; height: 96px; }} .small {{ width: 40px; height: 40px; }}
  .full {{ max-width: 90px; max-height: 96px; border-radius: 4px; }}
  h2 {{ font-size: 16px; margin: 10px 0 4px; }} p {{ margin: 4px 0; font-size: 13px; color: #94a3b8; }}
  .flag {{ color: #fbbf24; }} .credit {{ font-size: 12px; }} a {{ color: #38bdf8; }}
</style></head>
<body><h1>Photo review: {html.escape(sport_slug)}, {len(rows)} suggestions</h1>
<p>Circles show the photo as the game crops it, centred in the middle. Leave out a wrong person or an unusable photo
(a group shot, a face too small); a badly centred one can be kept and moved in the Control Room.</p>
<main>{''.join(cards)}</main></body></html>"""


# ============================================================
# WRITE
# ============================================================

def write_photo(player: dict, match: Match, credit: Credit) -> str:
    try:
        if api_request("GET", f"/api/admin/players/{player['id']}").get("photo"):
            return "skipped: it has a photo now"
        api_request("PUT", f"/api/admin/players/{player['id']}/photo", {
            "file": match.person.image, "credit": credit.credit, "license": credit.license,
            "focusX": 50, "focusY": 50, "source": "photo-review",
        })
    except (urllib.error.URLError, TimeoutError) as e:
        return f"not written: {describe_http_error(e)}"
    return "written"


def review(sport_slug: str, limit: int | None) -> None:
    if not ADMIN_KEY:
        sys.exit("ROSTER_ADMIN_KEY isn't set: photos are written through the Control Room's API and need its key.")
    print(f"Reading {describe_db_target()}; writing through the API at {API_URL}.")
    check_admin_key()

    players = load_players_without_photo(sport_slug)
    if not players:
        print("Every player of this tour has a photo.")
        return
    if limit:
        players = players[:limit]

    matches = look_up(players, PHOTO_PROFILES[sport_slug])
    with_photo = [(p, m) for p, m in matches if m.status == PHOTO]
    credits = fetch_credits(sorted({m.person.image for _, m in with_photo}))
    rows = [(p, m, credits[m.person.image]) for p, m in with_photo if m.person.image in credits]

    counts = {status: sum(1 for _, m in matches if m.status == status) for status in (PHOTO, NO_PHOTO, AMBIGUOUS, NOT_FOUND)}
    no_credit = len(with_photo) - len(rows)
    print(f"\n{len(rows)} photos to review. Of {len(players)} players: {counts[NO_PHOTO]} have no photo on Wikidata, "
          f"{counts[AMBIGUOUS]} matched more than one person, {counts[NOT_FOUND]} weren't found"
          + (f", {no_credit} {'photo' if no_credit == 1 else 'photos'} had no credit or licence on Commons."
             if no_credit else "."))
    for label, status in (("More than one match", AMBIGUOUS), ("Not found", NOT_FOUND)):
        names = [p["name"] for p, m in matches if m.status == status]
        if names:
            print(f"  {label} (set these by hand in the Control Room): {', '.join(names)}")
    if not rows:
        return

    page = Path(__file__).with_name(f"photo_review_{sport_slug}.html")
    page.write_text(review_page(sport_slug, rows), encoding="utf-8")
    print(f"\nReview page: {page}")
    webbrowser.open(page.as_uri())

    print()
    for number, (player, match, _) in enumerate(rows, start=1):
        flag = f"  [{match.flags[0]}]" if match.flags else ""
        print(f"  {number:3}. {player['name']}{flag}")

    chosen = set(range(1, len(rows) + 1))
    while True:
        if not chosen:
            print("\nNothing to write.")
            return
        print(f"\nReady to write {len(chosen)} of {len(rows)} photos.")
        choice = input("a = write them, r = leave some out, q = quit without writing: ").strip().lower()
        if choice == "q":
            return
        if choice == "r":
            numbers = parse_row_numbers(input("Numbers to leave out, e.g. 2 5: "), len(rows))
            if numbers is None:
                print(f"Type numbers between 1 and {len(rows)}.")
            else:
                chosen -= numbers
        elif choice == "a":
            break

    expected = db_write_confirmation()
    typed = input(f"Type '{expected}' to write {len(chosen)} photos to {describe_db_target()}: ").strip()
    if typed != expected:
        print("Not confirmed; nothing written.")
        return

    print()
    for number in sorted(chosen):
        player, match, credit = rows[number - 1]
        print(f"  {number}. {player['name']}: {write_photo(player, match, credit)}")


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Suggest a Wikimedia Commons photo for each player without one.")
    parser.add_argument("sport", choices=sorted(PHOTO_PROFILES), help="which tour")
    parser.add_argument("--limit", type=positive_int, help="look up at most N players this run")
    return parser


if __name__ == "__main__":
    args = build_arg_parser().parse_args()
    review(args.sport, args.limit)
