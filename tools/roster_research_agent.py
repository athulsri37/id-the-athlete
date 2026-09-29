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

USAGE
-----
    python roster_research_agent.py <sport> [count]

    sport  one of: cricket-men-international, cricket-women-international,
           tennis-men, tennis-women
    count  number of players to research, 1-30 (optional, defaults to 20)

    Example:
    python roster_research_agent.py tennis-men 10

    Run with --help to see the valid options. TIER_GUIDANCE in the CONFIG
    section below can still be edited to override a sport's default guidance.

REVIEW MENU
-----------
After research, the proposed players are shown in a numbered table with a
menu:
    a  accept and write all of them to the database
    r  remove players by number (e.g. 2,3); that many replacements are
       researched automatically
    s  search for more to reach the requested count (only shown when short)
    q  quit without writing anything
Before each replacement search you can type an optional note that is added
to that search's prompt only. Removed players are excluded for the rest of
the run but nothing is remembered between runs. The CSV log is rewritten
after every round so it always matches the current list, and nothing is
written to the database until you choose a.
"""

import argparse
import os
import sys
import json
import csv
import asyncio
import re
import unicodedata
from datetime import datetime
import psycopg2
from claude_agent_sdk import query, ClaudeAgentOptions, AssistantMessage, TextBlock

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
        "default_tier_guidance": (
            "Lean toward Medium/Hard-tier players: current or recent players with "
            "solid but not legendary careers (well under 10,000 runs, 300 wickets, "
            "or 300 matches), or genuine role-players/associate-nation cricketers."
        ),
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
        "default_tier_guidance": (
            "Lean toward Medium/Hard-tier players: current or recent players with "
            "solid but not legendary careers, or genuine role-players/associate-nation cricketers."
        ),
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
        "default_tier_guidance": (
            "Lean toward Medium/Hard-tier players: current or recent players who "
            "haven't reached #1 or won 20+ titles or 2+ Grand Slams, but have a "
            "real, recognizable career - a notable ranking peak, a signature win, "
            "or genuine current tour relevance."
        ),
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
        "default_tier_guidance": (
            "Lean toward Medium/Hard-tier players: current or recent players who "
            "haven't reached #1 or won 20+ titles or 2+ Grand Slams, but have a "
            "real, recognizable career - a notable ranking peak, a signature win, "
            "or genuine current tour relevance."
        ),
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
TIER_GUIDANCE = None  # leave as None to use that sport's default, or override with your own text

OUTPUT_FILE = f"proposed_batch_{datetime.now().strftime('%Y%m%d_%H%M%S')}.csv"

DB_CONFIG = {
    "host": "localhost",
    "port": 5432,
    "dbname": "idtheathlete",
    "user": "idtheathlete",
    "password": "idtheathlete_dev_pw",
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
        with psycopg2.connect(**DB_CONFIG) as conn:
            with conn.cursor() as cur:
                cur.execute(query_sql, (sport_slug,))
                return [row[0] for row in cur.fetchall()]
    except psycopg2.OperationalError as e:
        print(f"ERROR: Could not connect to the database. Is Docker/Postgres running?")
        print(f"Details: {e}")
        raise


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


def build_prompt(profile, batch_size, tier_guidance, existing_names, note=None):
    existing_list = "\n".join(f"- {name}" for name in existing_names)
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

TIER GUIDANCE:
{tier_guidance}
{note_line}
SOURCE REQUIREMENT: This SDK version does not support a hard technical
restriction to a single domain, so this is an explicit instruction instead -
treat {source_name} ({source_domain}) as the required source for every
statistic. Search specifically on {source_domain} for each player's profile
page, and cite the specific {source_domain} URL you used for each player's stats.
Only fall back to another source if a player genuinely has no profile on
{source_domain}, and clearly flag any player where you had to do this.

EXISTING ROSTER - DO NOT DUPLICATE ANY OF THESE NAMES:
{existing_list}

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


def save_players(players, output_path, fields):
    """Always writes the CSV, regardless of whether the DB write happens too -
    this is the permanent log, kept intentionally even in the direct-write flow."""
    with open(output_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for player in players:
            writer.writerow({field: player.get(field, "") for field in fields})


def print_table(players, fields):
    """Prints a clean, readable table to the terminal for the one-keypress
    review step, no external table-formatting library needed."""
    headers = ["#"] + list(fields)
    rows = [[str(i)] + [str(p.get(f, "")) for f in fields] for i, p in enumerate(players, start=1)]
    widths = [max([len(h)] + [len(row[c]) for row in rows]) for c, h in enumerate(headers)]

    header = " | ".join(h.ljust(widths[c]) for c, h in enumerate(headers))
    print("\n" + header)
    print("-" * len(header))
    for row in rows:
        print(" | ".join(cell.ljust(widths[c]) for c, cell in enumerate(row)))
    print()


def write_players_to_db(players, sport_slug, fields):
    """Writes players directly to the database in one transaction. If
    anything fails partway through, the whole batch is rolled back rather
    than left half-written."""
    with psycopg2.connect(**DB_CONFIG) as conn:
        with conn.cursor() as cur:
            cur.execute('SELECT "Id" FROM "Sports" WHERE "Slug" = %s', (sport_slug,))
            row = cur.fetchone()
            if row is None:
                raise ValueError(f"No sport found with slug '{sport_slug}'")
            sport_id = row[0]

            # Look up every AttributeDefinition ID for this sport once,
            # keyed by its Key column, so each player's inserts can reference
            # the correct AttributeDefinitionId.
            cur.execute(
                'SELECT "Key", "Id" FROM "AttributeDefinitions" WHERE "SportId" = %s',
                (sport_id,),
            )
            attr_ids = dict(cur.fetchall())

            for player in players:
                cur.execute(
                    'INSERT INTO "Players" ("Name", "SportId", "IsOverridden", "DifficultyOverride") '
                    'VALUES (%s, %s, false, NULL) RETURNING "Id"',
                    (player["name"], sport_id),
                )
                player_id = cur.fetchone()[0]

                for field in fields:
                    if field == "name":
                        continue
                    if field not in attr_ids:
                        raise ValueError(
                            f"No AttributeDefinition found for key '{field}' on this sport - "
                            f"aborting transaction, nothing will be written."
                        )
                    cur.execute(
                        'INSERT INTO "PlayerAttributeValues" '
                        '("PlayerId", "AttributeDefinitionId", "Value", "IsManuallyEdited") '
                        'VALUES (%s, %s, %s, false)',
                        (player_id, attr_ids[field], str(player.get(field, ""))),
                    )
        conn.commit()


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
    return parser


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
    tier_guidance = TIER_GUIDANCE or profile["default_tier_guidance"]
    fields = profile["fields"] + DISPLAY_ONLY_FIELDS

    print(f"Connecting to local database to load existing {profile['display_name']} roster...")
    existing_names = load_existing_names(sport_slug)
    print(f"Loaded {len(existing_names)} existing names to avoid duplicating.")

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
        # Rewrite the CSV log every round so it always matches the current list.
        save_players(players, OUTPUT_FILE, fields)
        print(f"\n{len(players)} of {target} players in the current list (logged to {OUTPUT_FILE}).")
        print_table(players, fields)

        print(f"  a  accept and write all {len(players)}")
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
            if not players:
                print("There are no players to write. Choose r, s or q.")
                continue
            if len(players) < target:
                print(f"\nWriting {len(players)} of the {target} requested.")
            try:
                write_players_to_db(players, sport_slug, profile["fields"])
                print(f"\nSuccess: {len(players)} players written to the database.")
            except Exception as e:
                print(f"\nERROR: Database write failed, nothing was committed: {e}")
                print(f"The CSV log at {OUTPUT_FILE} still has everything - safe to retry.")
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
