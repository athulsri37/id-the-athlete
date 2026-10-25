"""
Stat refresh for active players
===============================

Keeps active players' changing stats current (titles, rankings, matches,
runs, wickets, retirement) in two steps, both run by hand:

    python stat_refresh_agent.py research tennis-men [--limit 10]
    python stat_refresh_agent.py apply tennis-men

For production, run scripts/refresh-stats-prod.sh instead: it asks for the
database URL and admin key, backs production up before apply and offers to
copy production into dev afterwards.

RESEARCH only reads. It finds every Active player of the tour, asks the agent
for their current values in chunks of 10 (identified by ID, name, country and
turned-pro or debut year; the stored stats are not shown, so each value is
researched independently) and saves the answers to stat_refresh_<tour>.json
after every chunk. If it stops (usage limit, a failed chunk), run it again and
choose to continue. --limit researches only the first N players, for a trial.
Uses your Claude subscription, like the roster agent (same environment
checks).

APPLY re-reads every researched player from the database, checks each answer
(see stat_refresh.py: a total going down, a career high getting worse or a
comeback is never written; retirements and big jumps are flagged) and shows
the review table. Today's and yesterday's daily answers are held and written
by a later apply. You choose what to write, then type the database's name
(or "yes" locally). Each player is written through the Control Room's
player-update endpoint, so it's validated, logged as "Stat refresh" in the
Changes tab, keeps its difficulty override and gets a fresh trivia blurb. A
player edited since the research is skipped. Running apply again is safe:
written values are reported, not written twice.

Needs ROSTER_DB_URL (default: the local dev database), and for apply
ROSTER_API_URL and ROSTER_ADMIN_KEY: the same variables as the roster agent.
"""

import argparse
import asyncio
import json
import os
import sys
import urllib.error
import urllib.request
from contextlib import closing
from datetime import datetime, timedelta, timezone

import psycopg2

from roster_research_agent import (
    ADMIN_KEY,
    API_URL,
    CHUNK_SIZE,
    DB_URL,
    SPORT_PROFILES,
    UsageExhaustedError,
    call_agent,
    check_environment,
    db_write_confirmation,
    describe_db_target,
    parse_players,
)
from stat_refresh import (
    ACTIVE,
    RefreshCheck,
    UNSURE,
    check_refresh,
    held_players,
    match_answers,
    plan_apply,
    refreshable_keys,
    review_lines,
    same_name,
    search_name,
    shown_checks,
    tier_cell,
)

# What each refreshable column means, for the prompt. Keys are unique across
# sports, so one table covers every tour.
FIELD_NOTES = {
    "grand_slam_titles": "Grand Slam singles titles won (singles only)",
    "career_titles": "total tour-level singles titles won (singles only: no doubles, Challenger or ITF titles)",
    "career_high_ranking": "best singles ranking ever reached, 1 = world No. 1. The career high, NOT the current ranking",
    "combined_matches": "international matches played: Test + ODI + T20I added together",
    "combined_runs": "international runs scored: Test + ODI + T20I added together",
    "combined_wickets": "international wickets taken: Test + ODI + T20I added together",
    "active_status": 'exactly "Active" or "Retired"',
}

# Fields shown to the agent to tell same-name athletes apart. Never stats.
IDENTITY_KEYS = ["country", "turned_pro_year", "debut_year"]

USER_AGENT = "id-the-athlete-stat-refresh"


def proposal_path(sport_slug: str) -> str:
    return f"stat_refresh_{sport_slug}.json"


# ============================================================
# DATABASE (read only: every write goes through the API)
# ============================================================

def load_players(sport_slug: str) -> dict[int, dict]:
    """Every player of the tour: id -> {name, attributes, is_overridden}."""
    query_sql = """
        SELECT p."Id", p."Name", p."IsOverridden", ad."Key", v."Value"
        FROM "Players" p
        JOIN "Sports" s ON s."Id" = p."SportId"
        LEFT JOIN "PlayerAttributeValues" v ON v."PlayerId" = p."Id"
        LEFT JOIN "AttributeDefinitions" ad ON ad."Id" = v."AttributeDefinitionId"
        WHERE s."Slug" = %s
        ORDER BY p."Id";
    """
    players: dict[int, dict] = {}
    try:
        with closing(psycopg2.connect(DB_URL)) as conn, conn.cursor() as cur:
            cur.execute(query_sql, (sport_slug,))
            for player_id, name, overridden, key, value in cur.fetchall():
                player = players.setdefault(player_id, {"name": name, "attributes": {}, "is_overridden": overridden})
                if key is not None:
                    player["attributes"][key] = value
    except psycopg2.OperationalError as e:
        sys.exit(f"ERROR: couldn't connect to {describe_db_target()}. If it's the local one, is Docker/Postgres running?\n{e}")
    return players


def load_recent_puzzles(sport_slug: str, today) -> list[tuple]:
    """(date, player id) of the tour's daily puzzles from yesterday on."""
    query_sql = """
        SELECT d."PuzzleDate", d."PlayerId"
        FROM "DailyPuzzles" d
        JOIN "Sports" s ON s."Id" = d."SportId"
        WHERE s."Slug" = %s AND d."PuzzleDate" >= %s;
    """
    with closing(psycopg2.connect(DB_URL)) as conn, conn.cursor() as cur:
        cur.execute(query_sql, (sport_slug, today - timedelta(days=1)))
        return cur.fetchall()


# ============================================================
# ADMIN API
# ============================================================

def api_request(method: str, path: str, body=None):
    request = urllib.request.Request(
        f"{API_URL}{path}",
        data=None if body is None else json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json", "X-Admin-Key": ADMIN_KEY or "", "User-Agent": USER_AGENT},
        method=method,
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def describe_http_error(e: Exception) -> str:
    if isinstance(e, urllib.error.HTTPError):
        if e.code == 401:
            return "the admin key was rejected"
        try:
            detail = json.load(e).get("detail") or ""
        except (ValueError, AttributeError):
            detail = ""
        return f"the API answered {e.code}{': ' + detail if detail else ''}"
    return f"couldn't reach the API at {API_URL}: {e}"


def check_admin_key() -> None:
    """Stops before the review when the API can't be used, rather than
    failing on every player: repeated wrong keys also lock this PC out of
    the Control Room for a while."""
    try:
        api_request("GET", "/api/admin/sports")
    except (urllib.error.URLError, TimeoutError) as e:
        sys.exit(f"ERROR: can't use the Control Room's API: {describe_http_error(e)}. Nothing written.")


def fetch_tiers(sport_slug: str, stats: list[dict[str, str]]) -> list[str] | None:
    try:
        return api_request("POST", f"/api/admin/sports/{sport_slug}/difficulty-preview", stats)
    except (urllib.error.URLError, TimeoutError) as e:
        print(f"(Tier column skipped: {describe_http_error(e)}.)")
        return None


# ============================================================
# RESEARCH
# ============================================================

def build_refresh_prompt(profile: dict, keys: list[str], players: list[tuple[int, dict]]) -> str:
    lines = []
    for player_id, player in players:
        details = ", ".join(
            f"{key} {player['attributes'][key]}" for key in IDENTITY_KEYS if player["attributes"].get(key)
        )
        lines.append(f"- id {player_id}: {search_name(player['name'])}" + (f" ({details})" if details else ""))
    field_lines = "\n".join(f"- {key}: {FIELD_NOTES[key]}" for key in keys)

    return f"""You are checking the CURRENT career statistics of {profile["display_name"]} players
for a stats-guessing game. For each player below, find their values as of today,
using web search.

SOURCE REQUIREMENT: treat {profile["source_name"]} ({profile["source_domain"]}) as the
required source. Search {profile["source_domain"]} for each player's profile page and
cite it. Only use another source if a player genuinely has no profile there.

IDENTITY: identify each player by every detail given (name, country, year), not
the name alone: some players share a name with other athletes.

FIELDS:
{field_lines}

If you cannot confirm a value, answer "{UNSURE}" for that field. Never guess,
estimate or carry a number over from memory.

PLAYERS:
{chr(10).join(lines)}

Return a JSON array with one object per player above, with exactly these fields:
id, name, {", ".join(keys)}, source_url
Return ONLY the JSON array as your final output. You may reason about your
research beforehand, but the JSON array must be the very last thing in your
response, with nothing after it.
"""


def load_proposal(sport_slug: str) -> dict | None:
    path = proposal_path(sport_slug)
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save_proposal(proposal: dict) -> None:
    # Written to a temporary file first, so a crash mid-write can't leave
    # a half-written proposal behind.
    path = proposal_path(proposal["tour"])
    with open(path + ".tmp", "w", encoding="utf-8") as f:
        json.dump(proposal, f, indent=2, ensure_ascii=False)
    os.replace(path + ".tmp", path)


def new_proposal(sport_slug: str, limit: int | None) -> dict:
    players = load_players(sport_slug)
    active = [(pid, p) for pid, p in players.items() if p["attributes"].get("active_status") == ACTIVE]
    if limit is not None:
        active = active[:limit]
    keys = refreshable_keys({key for p in players.values() for key in p["attributes"]})
    return {
        "tour": sport_slug,
        "started": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "keys": keys,
        "players": [
            {
                "id": pid,
                "name": p["name"],
                "attributes": {k: p["attributes"][k] for k in keys + IDENTITY_KEYS if k in p["attributes"]},
                "answer": None,
            }
            for pid, p in active
        ],
    }


async def research(sport_slug: str, limit: int | None) -> None:
    check_environment()
    profile = SPORT_PROFILES[sport_slug]
    print(f"Reading {describe_db_target()}...")

    proposal = load_proposal(sport_slug)
    if proposal is not None:
        done = sum(1 for p in proposal["players"] if p["answer"] is not None)
        print(f"{proposal_path(sport_slug)} from {proposal['started']} has {done} of {len(proposal['players'])} players researched.")
        choice = input("Continue it (c), start a new refresh (n, replaces it), or quit (q)? ").strip().lower()
        if choice == "n":
            proposal = None
        elif choice != "c":
            return
    if proposal is None:
        proposal = new_proposal(sport_slug, limit)
        save_proposal(proposal)

    todo = [p for p in proposal["players"] if p["answer"] is None]
    print(f"{len(todo)} active players to research, {CHUNK_SIZE} per request.")
    for start in range(0, len(todo), CHUNK_SIZE):
        chunk = todo[start:start + CHUNK_SIZE]
        print(f"\n--- Players {start + 1}-{start + len(chunk)} of {len(todo)} ---")
        prompt = build_refresh_prompt(profile, proposal["keys"], [(p["id"], p) for p in chunk])
        try:
            answers = parse_players(await call_agent(prompt), required_key="id")
        except UsageExhaustedError as e:
            print(f"\nUSAGE LIMIT HIT: {e}\nProgress is saved: run research again later and choose c.")
            return
        except Exception as e:
            print(f"\nThis request failed: {e}\nProgress is saved: run research again and choose c.")
            return

        matched, problems = match_answers([p["id"] for p in chunk], answers)
        for problem in problems:
            print(f"  {problem}")
        for player in chunk:
            if player["id"] in matched:
                player["answer"] = matched[player["id"]]
        save_proposal(proposal)

    missing = sum(1 for p in proposal["players"] if p["answer"] is None)
    print(f"\nResearch saved to {proposal_path(sport_slug)}.")
    if missing:
        print(f"{missing} of them got no answer: run research again and choose c to retry them.")
    print(f"Next: python stat_refresh_agent.py apply {sport_slug}")


# ============================================================
# APPLY
# ============================================================

def checks_for(proposal: dict, live: dict[int, dict]) -> tuple[list[RefreshCheck], list[str]]:
    """Checks every researched player against the values the research saw,
    and notes the ones apply can't use."""
    checks, notes = [], []
    for player in proposal["players"]:
        answer = player["answer"]
        if answer is None:
            continue
        if player["id"] not in live:
            notes.append(f"{player['name']} (id {player['id']}) is no longer in this tour; skipped")
            continue
        before = {k: player["attributes"][k] for k in proposal["keys"] if k in player["attributes"]}
        check = check_refresh(player["id"], player["name"], before, answer)
        answered = str(answer.get("name", ""))
        if answered and not same_name(player["name"], answered):
            check.warnings.append(f"answered as '{answered}': check it's the same player")
        checks.append(check)
    return checks, notes


def review_tiers(sport_slug: str, checks: list[RefreshCheck], live: dict[int, dict]) -> dict[int, str]:
    shown = [c for c in shown_checks(checks) if c.changes]
    if not shown:
        return {}
    before = [dict(live[c.player_id]["attributes"]) for c in shown]
    after = [{**live[c.player_id]["attributes"], **{k: new for k, (_, new) in c.changes.items()}} for c in shown]
    tiers_before, tiers_after = fetch_tiers(sport_slug, before), fetch_tiers(sport_slug, after)
    return {
        c.player_id: tier_cell(
            tiers_before[i] if tiers_before else None,
            tiers_after[i] if tiers_after else None,
            live[c.player_id]["is_overridden"],
        )
        for i, c in enumerate(shown)
    }


def parse_row_numbers(raw: str, count: int) -> set[int] | None:
    try:
        numbers = {int(part) for part in raw.replace(",", " ").split()}
    except ValueError:
        return None
    return numbers if numbers and all(1 <= n <= count for n in numbers) else None


def write_player(check: RefreshCheck) -> str:
    """Writes one player's changes through the Control Room endpoint,
    re-checking the stored values first. Returns what happened."""
    try:
        detail = api_request("GET", f"/api/admin/players/{check.player_id}")
    except (urllib.error.URLError, TimeoutError) as e:
        return f"not written: {describe_http_error(e)}"

    now = {a["key"]: a["value"] for a in detail["attributes"]}
    plan = plan_apply(check.changes, now)
    if plan.conflicts:
        edited = ", ".join(f"{k} {before} -> {current}" for k, (before, current) in plan.conflicts.items())
        return f"skipped: edited since the research ({edited}); research it again next time"
    if not plan.write:
        return "already written"

    body = {
        "attributes": plan.write,
        # Sent back unchanged: the endpoint sets both, and leaving them out
        # would clear a hand-set difficulty override.
        "isOverridden": detail["isOverridden"],
        "difficultyOverride": detail.get("difficultyOverride"),
        "source": "stat-refresh",
    }
    try:
        api_request("PUT", f"/api/admin/players/{check.player_id}", body)
    except (urllib.error.URLError, TimeoutError) as e:
        return f"not written: {describe_http_error(e)}"
    return "written: " + "; ".join(f"{k} {v}" for k, v in plan.write.items())


def apply(sport_slug: str) -> None:
    proposal = load_proposal(sport_slug)
    if proposal is None:
        sys.exit(f"No research to apply: run 'python stat_refresh_agent.py research {sport_slug}' first.")
    if not ADMIN_KEY:
        sys.exit("ROSTER_ADMIN_KEY isn't set: apply writes through the Control Room's API and needs its key.")

    print(f"Reading {describe_db_target()}; writing through the API at {API_URL}.")
    check_admin_key()
    live = load_players(sport_slug)
    today = datetime.now(timezone.utc).date()
    held = held_players(load_recent_puzzles(sport_slug, today), today)

    checks, notes = checks_for(proposal, live)
    unresearched = sum(1 for p in proposal["players"] if p["answer"] is None)
    tiers = review_tiers(sport_slug, checks, live)

    print(f"\nResearch from {proposal['started']}, {len(checks)} players with answers:\n")
    for line in review_lines(checks, tiers, held):
        print(line)
    rows = shown_checks(checks)
    sources = {p["id"]: (p["answer"] or {}).get("source_url") for p in proposal["players"]}
    for i, check in enumerate(rows, start=1):
        if check.changes and sources.get(check.player_id):
            print(f"  source {i}: {sources[check.player_id]}")
    for note in notes:
        print(f"  {note}")
    if unresearched:
        print(f"  {unresearched} without an answer yet: run research again and choose c.")

    chosen = set()
    for i, check in enumerate(rows, start=1):
        if not check.is_valid or not check.changes or check.player_id in held:
            continue
        plan = plan_apply(check.changes, live[check.player_id]["attributes"])
        if plan.conflicts:
            edited = ", ".join(f"{k} {before} -> {now}" for k, (before, now) in plan.conflicts.items())
            print(f"  row {i}, {check.name}: edited since the research ({edited}); not written, research it again next time")
        elif not plan.write:
            print(f"  row {i}, {check.name}: already written")
        else:
            chosen.add(i)
    while True:
        if not chosen:
            print("\nNothing to write.")
            return
        print(f"\nReady to write {len(chosen)}: rows {', '.join(str(n) for n in sorted(chosen))}.")
        choice = input("a = write them, r = leave some rows out, q = quit without writing: ").strip().lower()
        if choice == "q":
            return
        if choice == "r":
            numbers = parse_row_numbers(input("Row numbers to leave out, e.g. 2 5: "), len(rows))
            if numbers is None:
                print(f"Type row numbers between 1 and {len(rows)}.")
            else:
                chosen -= numbers
        elif choice == "a":
            break

    expected = db_write_confirmation()
    typed = input(f"Type '{expected}' to write {len(chosen)} players to {describe_db_target()}: ").strip()
    if typed != expected:
        print("Not confirmed; nothing written.")
        return

    print()
    for i in sorted(chosen):
        check = rows[i - 1]
        print(f"  {i}. {check.name}: {write_player(check)}")

    if held:
        waiting = sorted({d for pid, d in held.items() if any(c.player_id == pid and c.changes for c in checks)})
        if waiting:
            print(f"\nHeld players: run apply again on or after {waiting[-1]:%d %b %Y} (UTC) to write them.")


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Refresh active players' stats: research, then apply.")
    parser.add_argument("step", choices=["research", "apply"])
    parser.add_argument("sport", choices=sorted(SPORT_PROFILES), help="which tour")
    parser.add_argument("--limit", type=positive_int, help="research only the first N active players (a trial)")
    return parser


def positive_int(value: str) -> int:
    try:
        number = int(value)
    except ValueError:
        number = 0
    if number < 1:
        raise argparse.ArgumentTypeError(f"must be a whole number of at least 1, got '{value}'")
    return number


if __name__ == "__main__":
    args = build_arg_parser().parse_args()
    if args.step == "research":
        asyncio.run(research(args.sport, args.limit))
    else:
        if args.limit is not None:
            sys.exit("--limit only applies to research.")
        apply(args.sport)
