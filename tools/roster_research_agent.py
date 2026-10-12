"""
Roster Research Agent
============================
Proposes a batch of new athlete candidates for ID the Athlete, researching
real career stats via web search, checking against existing roster names,
and writing a structured CSV log before an interactive review-and-write step.

Authenticates through your Claude Pro/Max subscription (via an OAuth token
from the official Claude CLI), not a separate pay-per-token API key. Usage
draws from your subscription's Agent SDK allowance.

After each run, you review the proposed players directly in your terminal
and choose whether to write them to the database - nothing is written
without that explicit confirmation.

SETUP
-----
1. Make sure the Claude CLI is installed (you already have this if you use
   Claude Code):
     npm install -g @anthropic-ai/claude-code

2. Generate a subscription OAuth token (opens a browser, one-time):
     claude setup-token
   This prints a token starting with sk-ant-oat...

3. Set that token as an environment variable:
     export CLAUDE_CODE_OAUTH_TOKEN="sk-ant-oat-..."

4. CRITICAL - if you have ANTHROPIC_API_KEY set from anything else (including
   the earlier version of this script), it will SILENTLY override the OAuth
   token with no error, and you'll be billed on your API balance again
   without realizing it. Clear it explicitly before running this:
     unset ANTHROPIC_API_KEY

5. pip install claude-agent-sdk psycopg2-binary

6. Make sure Docker/Postgres is running locally (docker compose up -d postgres
   from the project root).

7. For the difficulty tier column in the review table, have the API running
   (dotnet run in backend/IdTheAthlete.Api) and set your admin key:
     export ROSTER_ADMIN_KEY="<a value from your AdminKeys user-secrets>"
   Without it the agent still works; the tier column just shows "?".

ENVIRONMENT VARIABLES
---------------------
    ROSTER_DB_URL      database to read the roster from and write players to,
                       as a postgres URL. Defaults to the local Docker DB.
                       The target is always shown, and must be confirmed,
                       before anything is written.
    ROSTER_API_URL     the game API used for difficulty tiers
                       (default http://localhost:5080)
    ROSTER_ADMIN_KEY   admin key for that API (tiers are skipped without it)

USAGE
-----
    python roster_research_agent.py <sport> [count] [--tiers TIERS]

    sport    one of: cricket-men-international, cricket-women-international,
             tennis-men, tennis-women
    count    number of players to research, 1-30 (optional, defaults to 20)
    --tiers  difficulty tiers to aim for, any of easy,medium,hard separated
             by commas, or "any" for no preference (optional; defaults to
             medium, see DEFAULT_TIERS). This leans the research rather than
             enforcing it: players whose actual tier turns out different are
             marked "check" but can still be written.

    Examples:
    python roster_research_agent.py tennis-men 10                     (aims for medium)
    python roster_research_agent.py tennis-men 10 --tiers medium,hard
    python roster_research_agent.py tennis-men 10 --tiers any

    Run with --help to see the valid options. The wording used to describe
    each tier to the model is TIER_DESCRIPTIONS in the CONFIG section.

REVIEW MENU
-----------
After research, the proposed players are shown in a numbered table with a
menu:
    a  accept: write every player without errors to the database (after
       confirming the target); players with errors are skipped and listed
    r  remove players by number (e.g. 2,3); that many replacements are
       researched automatically
    s  search for more to reach the requested count (only shown when short)
    q  quit without writing anything
Before each replacement search you can type an optional note that is added
to that search's prompt only. Removed players are excluded for the rest of
the run but nothing is remembered between runs. The CSV log is rewritten
after every round so it always matches the current list, and nothing is
written to the database until you choose a.

CHECKS
------
Every proposed player is checked against the sport's columns in the
database (see roster_validation.py): missing values, stats that aren't whole
numbers, and values the game doesn't recognise (e.g. a role spelled
differently) are errors; a country new to the game is a warning, since the
closeness rules may need it added. The table shows each player's difficulty
tier, computed by the game's own rules through the API.
"""

import argparse
import os
import sys
import json
import csv
import asyncio
import re
import unicodedata
from contextlib import closing
import urllib.error
import urllib.request
from datetime import datetime
import psycopg2
from psycopg2.extensions import parse_dsn
from claude_agent_sdk import query, ClaudeAgentOptions, AssistantMessage, TextBlock

from roster_validation import (
    CATEGORICAL,
    NUMERIC,
    TIERS,
    AttributeSpec,
    ValidationResult,
    describe_tiers,
    parse_tiers,
    schema_mismatch,
    tier_warning,
    validate_player,
)

# ============================================================
# SAFETY CHECK - catch the silent-override problem before it happens.
# Called from main() after argument parsing, so --help works without
# either environment variable set.
# ============================================================

def check_environment():
    if os.environ.get("ANTHROPIC_API_KEY"):
        sys.exit(
            "ERROR: ANTHROPIC_API_KEY is currently set in this terminal session.\n"
            "It will silently override your subscription OAuth token and bill your\n"
            "separate API balance instead of your subscription, with no warning.\n\n"
            "Run this first, then try again:\n"
            "    unset ANTHROPIC_API_KEY"
        )

    if not os.environ.get("CLAUDE_CODE_OAUTH_TOKEN"):
        sys.exit(
            "ERROR: CLAUDE_CODE_OAUTH_TOKEN is not set.\n"
            "Run 'claude setup-token' to generate one, then:\n"
            '    export CLAUDE_CODE_OAUTH_TOKEN="sk-ant-oat-..."'
        )

# ============================================================
# SPORT PROFILES - one entry per sport, each defining the exact fields
# that sport's schema expects and how to explain them to the model.
# This is the only place sport-specific differences live - everything
# below this section is fully generic and works for any profile.
# ============================================================

SPORT_PROFILES = {
    "cricket-men-international": {
        "display_name": "Men's International Cricket",
        "source_name": "Cricinfo",
        "source_domain": "cricinfo.com",
        "fields": [
            "name", "country", "batting_hand", "bowling_style", "role",
            "combined_matches", "combined_runs", "combined_wickets",
            "debut_year", "active_status",
        ],
        "field_descriptions": """
- batting_hand: Right or Left
- bowling_style: Right-arm Pace, Right-arm Spin, Left-arm Pace, Left-arm Spin, or "Hasn't Bowled"
- role: Batter, Bowler, All-rounder, Batting All-rounder, Bowling All-rounder, or Wicketkeeper-Batter
- combined_matches/runs/wickets: Test + ODI + T20I added together
- debut_year: year of international debut
""",
        "min_appearances_note": "Do not include any player with fewer than 20 combined international appearances.",
    },
    "cricket-women-international": {
        "display_name": "Women's International Cricket",
        "source_name": "Cricinfo",
        "source_domain": "cricinfo.com",
        "fields": [
            "name", "country", "batting_hand", "bowling_style", "role",
            "combined_matches", "combined_runs", "combined_wickets",
            "debut_year", "active_status",
        ],
        "field_descriptions": """
- batting_hand: Right or Left
- bowling_style: Right-arm Pace, Right-arm Spin, Left-arm Pace, Left-arm Spin, or "Hasn't Bowled"
- role: Batter, Bowler, All-rounder, Batting All-rounder, Bowling All-rounder, or Wicketkeeper-Batter
- combined_matches/runs/wickets: Test + ODI + T20I added together
- debut_year: year of international debut
""",
        "min_appearances_note": "Do not include any player with fewer than 20 combined international appearances.",
    },
    "tennis-men": {
        "display_name": "Men's Tennis (ATP)",
        "source_name": "the official ATP Tour site",
        "source_domain": "atptour.com",
        "fields": [
            "name", "country", "plays", "backhand", "grand_slam_titles",
            "career_high_ranking", "turned_pro_year", "career_titles",
            "active_status",
        ],
        "field_descriptions": """
- plays: Right or Left (dominant hand)
- backhand: One-Handed or Two-Handed
- grand_slam_titles: number of Grand Slam singles titles won
- career_high_ranking: best ATP singles ranking ever achieved (1 = reached world #1)
- turned_pro_year: year turned professional
- career_titles: total ATP singles titles won
""",
        "min_appearances_note": "Only include players with a genuine, verifiable ATP tour career - not purely juniors or challengers-only players.",
    },
    "tennis-women": {
        "display_name": "Women's Tennis (WTA)",
        "source_name": "the official WTA site",
        "source_domain": "wtatennis.com",
        "fields": [
            "name", "country", "plays", "backhand", "grand_slam_titles",
            "career_high_ranking", "turned_pro_year", "career_titles",
            "active_status",
        ],
        "field_descriptions": """
- plays: Right or Left (dominant hand)
- backhand: One-Handed or Two-Handed
- grand_slam_titles: number of Grand Slam singles titles won
- career_high_ranking: best WTA singles ranking ever achieved (1 = reached world #1)
- turned_pro_year: year turned professional
- career_titles: total WTA singles titles won
""",
        "min_appearances_note": "Only include players with a genuine, verifiable WTA tour career - not purely juniors or challengers-only players.",
    },
}

# ============================================================
# CONFIG - edit this before each run
# ============================================================

# This field is for YOUR review only - it is never written to the database,
# since it isn't a real game attribute, just a way to verify sourcing.
DISPLAY_ONLY_FIELDS = ["source_url"]
CHUNK_SIZE = 10  # players requested per individual call
MAX_BATCH_SIZE = 30  # upper bound for the count argument
DEFAULT_BATCH_SIZE = 20  # used when count is omitted

OUTPUT_FILE = f"proposed_batch_{datetime.now().strftime('%Y%m%d_%H%M%S')}.csv"

# The local Docker database's development credentials (the same ones in
# docker-compose.yml) are only a default; set ROSTER_DB_URL for any other DB.
DB_URL = os.environ.get(
    "ROSTER_DB_URL", "postgresql://idtheathlete:idtheathlete_dev_pw@localhost:5432/idtheathlete"
)
API_URL = os.environ.get("ROSTER_API_URL", "http://localhost:5080").rstrip("/")
ADMIN_KEY = os.environ.get("ROSTER_ADMIN_KEY")

# Tiers aimed for when --tiers isn't given. Like any --tiers value this only
# leans the research: off-target players are flagged, never blocked.
DEFAULT_TIERS = frozenset({"medium"})

# How each tier is described to the model when --tiers is used. In words on
# purpose: the exact thresholds belong to the backend's rules, and every
# player's real tier is checked against them through the API afterwards.
TIER_DESCRIPTIONS = {
    "easy": "Easy: famous, widely recognised players - stars and all-time greats.",
    "medium": "Medium: established players a regular follower of the sport would know, "
    "with solid but not legendary careers.",
    "hard": "Hard: lesser-known players with a real, verifiable career - role players, "
    "journeymen, lower-ranked or associate-nation players.",
}

# ============================================================
# SCRIPT - no need to edit below this line
# ============================================================

def load_existing_names(sport_slug):
    query_sql = """
        SELECT p."Name" FROM "Players" p
        JOIN "Sports" s ON s."Id" = p."SportId"
        WHERE s."Slug" = %s
        ORDER BY p."Name";
    """
    try:
        with closing(psycopg2.connect(DB_URL)) as conn, conn.cursor() as cur:
            cur.execute(query_sql, (sport_slug,))
            return [row[0] for row in cur.fetchall()]
    except psycopg2.OperationalError as e:
        print(f"ERROR: Could not connect to the database. Is Docker/Postgres running?")
        print(f"Details: {e}")
        raise


def load_attribute_specs(sport_slug: str) -> list[AttributeSpec]:
    """The sport's clue columns from the database, each with the values its
    players already use (the accepted vocabulary for fixed-value columns)."""
    query_sql = """
        SELECT ad."Key", ad."Type", v."Value"
        FROM "AttributeDefinitions" ad
        JOIN "Sports" s ON s."Id" = ad."SportId"
        LEFT JOIN "PlayerAttributeValues" v ON v."AttributeDefinitionId" = ad."Id"
        WHERE s."Slug" = %s;
    """
    kinds: dict[str, str] = {}
    values: dict[str, set[str]] = {}
    with closing(psycopg2.connect(DB_URL)) as conn, conn.cursor() as cur:
        cur.execute(query_sql, (sport_slug,))
        for key, attribute_type, value in cur.fetchall():
            # AttributeType enum in the backend: 0 = Categorical, 1 = Numeric.
            kinds[key] = NUMERIC if attribute_type == 1 else CATEGORICAL
            if value is not None and attribute_type != 1:
                values.setdefault(key, set()).add(value)

    return [AttributeSpec(key, kind, frozenset(values.get(key, ()))) for key, kind in sorted(kinds.items())]


def describe_db_target() -> str:
    """The database the agent writes to, without the password."""
    params = parse_dsn(DB_URL)
    return f"database '{params.get('dbname', '?')}' on {params.get('host', 'localhost')}:{params.get('port', '5432')}"


def fetch_tiers(sport_slug: str, results: list[ValidationResult]) -> list[str] | None:
    """Difficulty tiers for the players, computed by the game's own rules via
    the API. None (with the reason printed) when the API can't be used, since
    the tier is informational and shouldn't stop a review."""
    if not ADMIN_KEY:
        print("(Tier column skipped: set ROSTER_ADMIN_KEY and run the API to see tiers.)")
        return None

    request = urllib.request.Request(
        f"{API_URL}/api/admin/sports/{sport_slug}/difficulty-preview",
        data=json.dumps([r.values for r in results]).encode("utf-8"),
        headers={"Content-Type": "application/json", "X-Admin-Key": ADMIN_KEY},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            return json.load(response)
    except urllib.error.HTTPError as e:
        reason = "the admin key was rejected" if e.code == 401 else f"the API answered {e.code}"
        print(f"(Tier column skipped: {reason}.)")
    except (urllib.error.URLError, TimeoutError) as e:
        print(f"(Tier column skipped: couldn't reach the API at {API_URL}: {e}.)")
    return None


REVIEWER_NOTE_MARKER = "REVIEWER NOTE:"
REVIEWER_NOTE_INSTRUCTIONS = (
    "Because the reviewer gave guidance for this search, include exactly one line in your\n"
    "response, before the JSON array, starting with \"REVIEWER NOTE:\". If you fully followed\n"
    "the guidance, write \"REVIEWER NOTE: followed.\" If you could not fully follow it, write\n"
    "\"REVIEWER NOTE: could not fully follow, \" and then one plain sentence naming the player\n"
    "or requirement and the reason, for example because a requested player is already in the\n"
    "roster, was already proposed or removed this run, or has fewer than the minimum\n"
    "appearances. Do not put square brackets in this line."
)


MARKDOWN_PREFIX = re.compile(r"^[\s*_\->#`]+")
COMMENT_EDGE_CHARS = " \t\r\n*_`"


def extract_reviewer_notes(raw_text):
    """Returns the text after "REVIEWER NOTE:" on each line that starts with
    it, matched case-insensitively. Markdown the model may wrap around the
    line (bold, bullets, quotes, headings, code ticks) is ignored; a marker
    in the middle of a line is not matched."""
    notes = []
    for line in raw_text.splitlines():
        stripped = MARKDOWN_PREFIX.sub("", line)
        if stripped.lower().startswith(REVIEWER_NOTE_MARKER.lower()):
            notes.append(stripped[len(REVIEWER_NOTE_MARKER):].strip(COMMENT_EDGE_CHARS))
    return notes


def build_tier_guidance(wanted: frozenset[str] | None) -> str | None:
    """Prompt text steering research towards the tiers asked for with --tiers,
    or None for no preference. Deliberately describes the tiers in words
    rather than the formula's thresholds, which live only in the backend;
    each player's real tier is checked through the API after research."""
    if not wanted:
        return None
    lines = [TIER_DESCRIPTIONS[tier] for tier in TIERS if tier in wanted]
    return "Aim for players who fit these difficulty tiers:\n- " + "\n- ".join(lines)


def build_prompt(profile, batch_size, tier_guidance, existing_names, note=None):
    existing_list = "\n".join(f"- {name}" for name in existing_names)
    tier_section = f"\nTIER GUIDANCE:\n{tier_guidance}\n" if tier_guidance else ""
    sport = profile["display_name"]
    source_name = profile["source_name"]
    source_domain = profile["source_domain"]
    fields = profile["fields"] + DISPLAY_ONLY_FIELDS
    note_line = (
        f"\nAdditional guidance from the reviewer for this search: {note}\n"
        f"\n{REVIEWER_NOTE_INSTRUCTIONS}\n"
        if note
        else ""
    )

    return f"""You are researching real athletes for a stats-guessing game covering {sport}.

TASK: Propose exactly {batch_size} real, currently or recently active {sport} players
who are NOT already in the existing roster below. For each player, research their
actual career statistics using web search - do not estimate or guess.
{tier_section}{note_line}
SOURCE REQUIREMENT: This SDK version does not support a hard technical
restriction to a single domain, so this is an explicit instruction instead -
treat {source_name} ({source_domain}) as the required source for every
statistic. Search specifically on {source_domain} for each player's profile
page, and cite the specific {source_domain} URL you used for each player's stats.
Only fall back to another source if a player genuinely has no profile on
{source_domain}, and clearly flag any player where you had to do this.

EXISTING ROSTER - DO NOT DUPLICATE ANY OF THESE NAMES:
{existing_list}

SAME-NAME PLAYERS: player names must be unique within this roster. If a player
you want to propose is a DIFFERENT person who shares a name with someone in the
roster above (or with another player you are proposing), give them a
disambiguated name by appending their country in parentheses, exactly as it
appears in the country field, e.g. "John Smith (New Zealand)". If they also
share a country, append their birth year instead, e.g. "John Smith (b. 1985)".
Never do this for someone who is already in the roster - that is a duplicate.

For each of the {batch_size} players, verify via web search the following fields:
{profile["field_descriptions"]}

IMPORTANT ACCURACY NOTES:
- Cite your source for each player's stats where possible.
- If you are not confident in a specific number, say so explicitly rather
  than inventing a plausible-sounding figure.
- {profile["min_appearances_note"]}
- The active_status field must be EXACTLY "Active" or "Retired" - no extra
  detail, dates, or parenthetical notes added to that value.
- Include a source_url field for every player: the specific {source_domain}
  profile page URL you used to verify their stats. If you had to use a
  different source because no {source_domain} profile exists, put that
  source's URL here instead and flag this exception clearly in your
  reasoning text before the JSON.
- Double check your final count is exactly {batch_size} names before finishing.

Return your answer as a JSON array, one object per player, with exactly these
fields: {", ".join(fields)}
Return ONLY the JSON array as your final output. You may reason about your
research process beforehand, but the JSON array itself must be the very last
thing in your response, with nothing after it.
"""


class UsageExhaustedError(Exception):
    """Raised when a failure looks like subscription usage/credits running
    out, rather than a generic transient error. Signals the caller to stop
    attempting further chunks entirely, rather than retrying something that
    will just fail again."""
    pass


USAGE_EXHAUSTED_KEYWORDS = ["credit", "quota", "billing", "usage limit", "insufficient"]
RETRY_BACKOFF_SCHEDULE = [1.0, 3.0]  # seconds - wait before attempt 2, then attempt 3
MAX_ATTEMPTS = 3


async def call_agent(prompt):
    """Runs one query through the Agent SDK, authenticated via subscription
    OAuth (CLAUDE_CODE_OAUTH_TOKEN), and returns the concatenated text of
    the response.

    NOTE on permissions: this SDK wraps Claude Code, which normally expects
    a human present to approve each tool use interactively. In a headless
    script with nobody there to click "allow," permission_mode="bypassPermissions"
    is needed to let tool calls (like web_search) proceed automatically.
    Be aware this grants broader tool access than just web_search alone, there
    is no simpler "approve only this one tool" option in the basic config.
    For a local, read-only research script this is a reasonable tradeoff, but
    worth knowing explicitly rather than assuming it's narrowly scoped.

    NOTE on error handling: this SDK has had documented issues (see public
    GitHub reports against claude-agent-sdk-python) where rate-limit-related
    events can crash the query() stream in ways that are hard to distinguish
    cleanly from other failures, and in rare cases may not be fully catchable
    from calling code at all. The retry/detection logic below handles what
    genuinely is catchable - it cannot guarantee protection against every
    possible failure mode this SDK might have.
    """
    options = ClaudeAgentOptions(
        model="claude-sonnet-5",
        allowed_tools=["web_search"],
        max_turns=15,  # allows enough back-and-forth for multi-player research
        permission_mode="bypassPermissions",
    )

    for attempt in range(1, MAX_ATTEMPTS + 1):
        try:
            text_parts = []
            async for message in query(prompt=prompt, options=options):
                if isinstance(message, AssistantMessage):
                    for block in message.content:
                        if isinstance(block, TextBlock):
                            text_parts.append(block.text)
            return "".join(text_parts)

        except Exception as e:
            error_text = str(e).lower()

            if any(keyword in error_text for keyword in USAGE_EXHAUSTED_KEYWORDS):
                # Don't retry this - it will just fail again. Signal the
                # caller to stop the whole run, not just this chunk.
                raise UsageExhaustedError(
                    f"This looks like a usage/credit limit, not a transient error: {e}"
                )

            print(f"  Attempt {attempt}/{MAX_ATTEMPTS} failed: {e}")
            if attempt < MAX_ATTEMPTS:
                wait_time = RETRY_BACKOFF_SCHEDULE[attempt - 1]
                print(f"  Retrying in {wait_time}s...")
                await asyncio.sleep(wait_time)
            else:
                raise  # exhausted retries, let the caller decide what to do


def parse_players(raw_text):
    # The response often contains other bracketed text (source lists,
    # markdown links, asides) before or after the player array, so try
    # decoding at every "[" and keep the last list that looks like players.
    decoder = json.JSONDecoder()
    found = None
    pos = raw_text.find("[")
    while pos != -1:
        try:
            value, _ = decoder.raw_decode(raw_text, pos)
        except json.JSONDecodeError:
            value = None
        if (
            isinstance(value, list)
            and value
            and all(isinstance(item, dict) and "name" in item for item in value)
        ):
            found = value
        pos = raw_text.find("[", pos + 1)

    if found is None:
        print("ERROR: Could not find a JSON array of players anywhere in the model's response.")
        print("Raw response has been saved to raw_output.txt for inspection.")
        with open("raw_output.txt", "w", encoding="utf-8") as f:
            f.write(raw_text)
        raise ValueError("No JSON array of players found in response")

    return found


def normalize_name(name):
    s = unicodedata.normalize("NFKD", name)
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = s.casefold()
    s = re.sub(r"[^a-z0-9]+", " ", s)
    return " ".join(s.split())


def filter_duplicates(chunk_players, seen_keys):
    """Drops players whose normalized name is already in seen_keys, and adds
    each kept player's key to seen_keys so repeats later in the same chunk
    (or in later chunks) are caught too. Mutates seen_keys."""
    kept_players = []
    dropped_names = []
    for player in chunk_players:
        key = normalize_name(str(player["name"]))
        if key in seen_keys:
            dropped_names.append(player["name"])
        else:
            seen_keys.add(key)
            kept_players.append(player)
    return kept_players, dropped_names


def save_players(
    players: list[dict], results: list[ValidationResult], tiers: list[str] | None, output_path: str, fields: list[str]
) -> None:
    """Always writes the CSV, regardless of whether the DB write happens too -
    this is the permanent log of what was proposed, with each player's tier
    and any problems the checks found."""
    columns = list(fields) + ["tier", "issues"]
    with open(output_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=columns)
        writer.writeheader()
        for i, (player, result) in enumerate(zip(players, results)):
            row = {field: player.get(field, "") for field in fields}
            row["tier"] = tiers[i] if tiers else ""
            row["issues"] = "; ".join(result.errors + result.warnings)
            writer.writerow(row)


def print_table(
    players: list[dict], results: list[ValidationResult], tiers: list[str] | None, fields: list[str]
) -> None:
    """Prints a clean, readable table to the terminal for the review step, no
    external table-formatting library needed, followed by every problem the
    checks found. Status is "ERROR" (won't be written), "check" (will be
    written, but look at the note) or "ok"."""
    headers = ["#"] + list(fields) + ["tier", "status"]
    rows = []
    for i, (player, result) in enumerate(zip(players, results), start=1):
        tier = tiers[i - 1] if tiers else "?"
        status = "ERROR" if result.errors else "check" if result.warnings else "ok"
        # Show the value that will be written (e.g. "india" corrected to the
        # game's "India"), falling back to what the model returned.
        cells = [str(result.values.get(f, player.get(f, ""))) for f in fields]
        rows.append([str(i)] + cells + [tier, status])
    widths = [max([len(h)] + [len(row[c]) for row in rows]) for c, h in enumerate(headers)]

    header = " | ".join(h.ljust(widths[c]) for c, h in enumerate(headers))
    print()
    print(header)
    print("-" * len(header))
    for row in rows:
        print(" | ".join(cell.ljust(widths[c]) for c, cell in enumerate(row)))

    for i, result in enumerate(results, start=1):
        if result.errors or result.warnings:
            print(f"  {i}. {result.name or '(no name)'}:")
            for error in result.errors:
                print(f"       error: {error}")
            for warning in result.warnings:
                print(f"       check: {warning}")
    print()


def write_players_to_db(results: list[ValidationResult], sport_slug: str) -> None:
    """Writes validated players to the database in one transaction, using their
    cleaned values. If anything fails partway through, the whole batch is
    rolled back rather than left half-written."""
    with closing(psycopg2.connect(DB_URL)) as conn:
        with conn, conn.cursor() as cur:  # commits on success, rolls back on any error
            cur.execute('SELECT "Id" FROM "Sports" WHERE "Slug" = %s', (sport_slug,))
            row = cur.fetchone()
            if row is None:
                raise ValueError(f"No sport found with slug '{sport_slug}'")
            sport_id = row[0]

            cur.execute('SELECT "Key", "Id" FROM "AttributeDefinitions" WHERE "SportId" = %s', (sport_id,))
            attr_ids = dict(cur.fetchall())

            for result in results:
                cur.execute(
                    'INSERT INTO "Players" ("Name", "SportId", "IsOverridden", "DifficultyOverride") '
                    'VALUES (%s, %s, false, NULL) RETURNING "Id"',
                    (result.name, sport_id),
                )
                player_id = cur.fetchone()[0]
                for key, value in result.values.items():
                    cur.execute(
                        'INSERT INTO "PlayerAttributeValues" '
                        '("PlayerId", "AttributeDefinitionId", "Value", "IsManuallyEdited") '
                        'VALUES (%s, %s, %s, false)',
                        (player_id, attr_ids[key], value),
                    )


def batch_count(value):
    try:
        count = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError(f"count must be an integer, got '{value}'")
    if not 1 <= count <= MAX_BATCH_SIZE:
        raise argparse.ArgumentTypeError(
            f"count must be between 1 and {MAX_BATCH_SIZE} inclusive, got {count}"
        )
    return count


def build_arg_parser():
    parser = argparse.ArgumentParser(
        description="Research new athlete candidates for a sport and review them before writing to the database."
    )
    parser.add_argument(
        "sport",
        choices=list(SPORT_PROFILES.keys()),
        help="which sport/tour to research players for",
    )
    parser.add_argument(
        "count",
        nargs="?",
        type=batch_count,
        default=DEFAULT_BATCH_SIZE,
        help=f"number of players to research (1-{MAX_BATCH_SIZE}, default {DEFAULT_BATCH_SIZE})",
    )
    parser.add_argument(
        "--tiers",
        type=tiers_arg,
        default=DEFAULT_TIERS,
        help=f"difficulty tiers to aim for, comma-separated, e.g. medium,hard, or 'any' for no preference "
        f"(default: {describe_tiers(DEFAULT_TIERS)}). Players whose actual tier differs are flagged in "
        "the review table.",
    )
    return parser


def tiers_arg(value: str) -> frozenset[str]:
    try:
        return parse_tiers(value)
    except ValueError as e:
        raise argparse.ArgumentTypeError(str(e))


async def research_players(profile, count, tier_guidance, exclusion_names, seen_keys, note=None):
    """Runs the chunked research loop for `count` players. The initial search
    and every replacement search both go through here so they behave the
    same. Mutates seen_keys via filter_duplicates. Returns
    (players, duplicates_dropped, agent_shortfall, status, reviewer_notes)
    where status is "ok", "usage_exhausted" or "chunk_failed", and
    reviewer_notes has one (chunk_num, [comment lines]) entry per chunk that
    got a response, collected only when a note was given."""
    duplicates_dropped = 0
    agent_shortfall = 0
    status = "ok"
    reviewer_notes = []

    all_players = []
    remaining = count
    chunk_num = 1

    while remaining > 0:
        this_chunk_size = min(CHUNK_SIZE, remaining)
        print(f"\n--- Chunk {chunk_num}: requesting {this_chunk_size} players ---")

        exclusion_list = exclusion_names + [p.get("name", "") for p in all_players]
        prompt = build_prompt(profile, this_chunk_size, tier_guidance, exclusion_list, note)

        try:
            raw_response = await call_agent(prompt)
        except UsageExhaustedError as e:
            print(f"\nUSAGE LIMIT HIT: {e}")
            print(f"Stopping here - {len(all_players)} players from earlier")
            print("successful chunks will still be saved. This is not a bug,")
            print("your subscription usage or credits appear to be exhausted")
            print("for now. Try again later, or check your account.")
            status = "usage_exhausted"
            break
        except Exception as e:
            print(f"\nChunk {chunk_num} failed after all retries: {e}")
            print(f"Stopping here - {len(all_players)} players from earlier")
            print("successful chunks will still be saved.")
            status = "chunk_failed"
            break

        if note:
            reviewer_notes.append((chunk_num, extract_reviewer_notes(raw_response)))

        try:
            chunk_players = parse_players(raw_response)
        except (ValueError, json.JSONDecodeError):
            print(f"Chunk {chunk_num} failed to parse. Stopping here - {len(all_players)}")
            print("players from earlier successful chunks will still be saved.")
            status = "chunk_failed"
            break

        chunk_shortfall = max(0, this_chunk_size - len(chunk_players))
        if chunk_shortfall:
            print(f"Chunk {chunk_num}: agent returned {len(chunk_players)} of {this_chunk_size} requested.")
        agent_shortfall += chunk_shortfall

        kept_players, dropped_names = filter_duplicates(chunk_players, seen_keys)
        for name in dropped_names:
            print(f"Dropped duplicate, already in roster or already proposed this run: {name}")
        duplicates_dropped += len(dropped_names)

        print(f"Chunk {chunk_num} succeeded: {len(kept_players)} players.")
        all_players.extend(kept_players)
        remaining -= this_chunk_size
        chunk_num += 1

    return all_players, duplicates_dropped, agent_shortfall, status, reviewer_notes


def report_reviewer_notes(reviewer_notes):
    """Prints what the agent said about the reviewer's note for one search.
    reviewer_notes has one entry per chunk that returned a response, so an
    empty list means nothing came back and there is nothing to judge."""
    if not reviewer_notes:
        return
    print("\nAgent comment on your note:")
    lines = [(chunk, text) for chunk, texts in reviewer_notes for text in texts]
    if not lines:
        print("The agent gave no comment on your note. Check the results below against what you asked for.")
        return
    label_chunks = len(reviewer_notes) > 1
    for chunk, text in lines:
        print(f"  Chunk {chunk}: {text}" if label_chunks else f"  {text}")


def report_search(requested, found, duplicates_dropped, agent_shortfall, status):
    """Prints one search's result, reusing the earlier shortfall wording."""
    print(f"\nSearch found {len(found)} of {requested} requested.")
    if len(found) >= requested:
        return
    if status != "ok":
        print(f"(Requested {requested}, but a chunk failed partway through - see above.)")
        return
    reasons = []
    if duplicates_dropped > 0:
        reasons.append(f"{duplicates_dropped} duplicate(s) dropped")
    if agent_shortfall > 0:
        reasons.append(f"the agent returned {agent_shortfall} fewer than requested")
    joined = " and ".join(reasons)
    explanation = f" {joined[0].upper()}{joined[1:]}." if reasons else ""
    print(f"(Requested {requested}, got {len(found)}.{explanation} Choose s to search for more.)")


def parse_removal(raw, count):
    """Returns (valid 1-based positions, rejected entries)."""
    valid, rejected = [], []
    for token in re.split(r"[,\s]+", raw.strip()):
        if not token:
            continue
        if token.isdigit() and 1 <= int(token) <= count:
            if int(token) not in valid:
                valid.append(int(token))
        else:
            rejected.append(token)
    return valid, rejected


async def main(args):
    check_environment()

    sport_slug = args.sport
    target = args.count
    profile = SPORT_PROFILES[sport_slug]
    wanted_tiers = args.tiers
    tier_guidance = build_tier_guidance(wanted_tiers)
    fields = profile["fields"] + DISPLAY_ONLY_FIELDS
    print(f"Aiming for {describe_tiers(wanted_tiers)} players." if wanted_tiers else "No tier preference.")

    print(f"Connecting to {describe_db_target()} to load the existing {profile['display_name']} roster...")
    existing_names = load_existing_names(sport_slug)
    print(f"Loaded {len(existing_names)} existing names to avoid duplicating.")

    # Checked before any research is paid for: the fields the agent asks for
    # must be exactly the sport's columns, or players couldn't be written.
    specs = load_attribute_specs(sport_slug)
    problems = schema_mismatch(profile["fields"], specs)
    if problems:
        sys.exit(
            "ERROR: this sport's fields in SPORT_PROFILES don't match its columns in the database:\n  "
            + "\n  ".join(problems)
        )

    # Keys are never removed from seen_keys, so a player the reviewer removes
    # stays excluded for the rest of this run. Nothing persists between runs.
    seen_keys = {normalize_name(name) for name in existing_names}
    proposed_names = []
    players = []

    async def search(count, note=None):
        found, dropped, shortfall, status, reviewer_notes = await research_players(
            profile, count, tier_guidance, existing_names + proposed_names, seen_keys, note
        )
        proposed_names.extend(p.get("name", "") for p in found)
        players.extend(found)
        report_search(count, found, dropped, shortfall, status)
        if note:
            report_reviewer_notes(reviewer_notes)

    def ask_note():
        return input("Optional note for this search, or press Enter to skip: ").strip() or None

    await search(target)

    while True:
        results = [validate_player(p, specs) for p in players]
        valid = [r for r in results if r.is_valid]
        tiers = fetch_tiers(sport_slug, results) if players else None
        if tiers:
            # A player with errors has unreliable stats, so its tier is too.
            tiers = [tier if result.is_valid else "-" for tier, result in zip(tiers, results)]
            for tier, result in zip(tiers, results):
                warning = tier_warning(tier, wanted_tiers)
                if warning:
                    result.warnings.append(warning)

        # Rewrite the CSV log every round so it always matches the current list.
        save_players(players, results, tiers, OUTPUT_FILE, fields)
        print(f"\n{len(players)} of {target} players in the current list (logged to {OUTPUT_FILE}).")
        print_table(players, results, tiers, fields)

        skipped_note = f", skipping {len(players) - len(valid)} with errors" if len(valid) < len(players) else ""
        print(f"  a  accept and write {len(valid)}{skipped_note}")
        print("  r  remove players by number, for example 2,3")
        if len(players) < target:
            print(f"  s  search for {target - len(players)} more to reach {target}")
        print("  q  quit without writing")

        while True:
            choice = input("Choose: ").strip().lower()
            if choice in ("a", "r", "q") or (choice == "s" and len(players) < target):
                break
            print("Please choose one of the options shown.")

        if choice == "q":
            print(f"\nNothing written to the database. CSV log saved at {OUTPUT_FILE}.")
            return

        if choice == "a":
            if not valid:
                print("No player can be written (every one has an error). Choose r, s or q.")
                continue
            confirm = input(f"Write {len(valid)} players to {describe_db_target()}? Type yes to confirm: ")
            if confirm.strip().lower() != "yes":
                print("Nothing written. Back to the review menu.")
                continue
            try:
                write_players_to_db(valid, sport_slug)
                print(f"\nSuccess: {len(valid)} players written to {describe_db_target()}.")
            except Exception as e:
                print(f"\nERROR: Database write failed, nothing was committed: {e}")
                print(f"The CSV log at {OUTPUT_FILE} still has everything - safe to retry.")
                return
            skipped = [r for r in results if not r.is_valid]
            if skipped:
                print(f"Skipped {len(skipped)} with errors (details in {OUTPUT_FILE}):")
                for result in skipped:
                    print(f"  {result.name or '(no name)'}: {result.errors[0]}")
            return

        if choice == "r":
            raw = input("Numbers to remove, separated by commas or spaces: ")
            valid, rejected = parse_removal(raw, len(players))
            if rejected:
                print(f"Ignored invalid or out-of-range entries: {', '.join(rejected)}")
            if not valid:
                print("No valid player numbers given. Nothing removed.")
                continue
            for i in valid:
                print(f"Removed: {players[i - 1].get('name', '')}")
            players[:] = [p for i, p in enumerate(players, start=1) if i not in valid]
            await search(target - len(players), ask_note())
            continue

        if choice == "s":
            await search(target - len(players), ask_note())


if __name__ == "__main__":
    args = build_arg_parser().parse_args()
    asyncio.run(main(args))
