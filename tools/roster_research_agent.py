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
    python roster_research_agent.py

Then edit the CONFIG section below before each run to describe what you want.
"""

import os
import sys
import json
import csv
import asyncio
from datetime import datetime
import psycopg2
from claude_agent_sdk import query, ClaudeAgentOptions, AssistantMessage, TextBlock

# ============================================================
# SAFETY CHECK - catch the silent-override problem before it happens
# ============================================================

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

ACTIVE_SPORT_SLUG = "cricket-men-international"  # pick a key from SPORT_PROFILES above

# This field is for YOUR review only - it is never written to the database,
# since it isn't a real game attribute, just a way to verify sourcing.
DISPLAY_ONLY_FIELDS = ["source_url"]
BATCH_SIZE = 30
CHUNK_SIZE = 10  # players requested per individual call
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


def build_prompt(profile, batch_size, tier_guidance, existing_names):
    existing_list = "\n".join(f"- {name}" for name in existing_names)
    sport = profile["display_name"]
    fields = profile["fields"] + DISPLAY_ONLY_FIELDS

    return f"""You are researching real athletes for a stats-guessing game covering {sport}.

TASK: Propose exactly {batch_size} real, currently or recently active {sport} players
who are NOT already in the existing roster below. For each player, research their
actual career statistics using web search - do not estimate or guess.

TIER GUIDANCE:
{tier_guidance}

SOURCE REQUIREMENT: This SDK version does not support a hard technical
restriction to a single domain, so this is an explicit instruction instead -
treat ESPNcricinfo (espncricinfo.com) as the required source for every
statistic. Search specifically on espncricinfo.com for each player's profile
page, and cite the specific ESPNcricinfo URL you used for each player's stats.
Only fall back to another source if a player genuinely has no ESPNcricinfo
profile, and clearly flag any player where you had to do this.

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
- Include a source_url field for every player: the specific ESPNcricinfo
  profile page URL you used to verify their stats. If you had to use a
  different source because no ESPNcricinfo profile exists, put that source's
  URL here instead and make sure active_status or another field doesn't need
  a note - flag this exception clearly in your reasoning text before the JSON.
- Double check your final count is exactly {batch_size} names before finishing.

Return your answer as a JSON array, one object per player, with exactly these
fields: {", ".join(fields)}
Return ONLY the JSON array as your final output. You may reason about your
research process beforehand, but the JSON array itself must be the very last
thing in your response, with nothing after it.
"""


async def call_agent(prompt):
    """Runs one query through the Agent SDK, authenticated via subscription
    OAuth (CLAUDE_CODE_OAUTH_TOKEN), and returns the concatenated text of
    the response.

    NOTE on permissions: this SDK wraps Claude Code, which normally expects
    a human present to approve each tool use interactively. In a headless
    script with nobody there to click "allow," permission_mode="bypassPermissions"
    plus the required allow_dangerously_skip_permissions=True flag are both
    needed together to let tool calls (like web_search) proceed automatically.
    Be aware this grants broader tool access than just web_search alone, there
    is no simpler "approve only this one tool" option in the basic config.
    For a local, read-only research script this is a reasonable tradeoff, but
    worth knowing explicitly rather than assuming it's narrowly scoped.
    """
    options = ClaudeAgentOptions(
        model="claude-sonnet-5",
        allowed_tools=["web_search"],
        max_turns=15,  # allows enough back-and-forth for multi-player research
        permission_mode="bypassPermissions",
    )

    text_parts = []
    async for message in query(prompt=prompt, options=options):
        if isinstance(message, AssistantMessage):
            for block in message.content:
                if isinstance(block, TextBlock):
                    text_parts.append(block.text)

    return "".join(text_parts)


def parse_players(raw_text):
    start = raw_text.find("[")
    end = raw_text.rfind("]")

    if start == -1 or end == -1 or end < start:
        print("ERROR: Could not find a JSON array anywhere in the model's response.")
        print("Raw response has been saved to raw_output.txt for inspection.")
        with open("raw_output.txt", "w", encoding="utf-8") as f:
            f.write(raw_text)
        raise ValueError("No JSON array found in response")

    cleaned = raw_text[start:end + 1]

    try:
        return json.loads(cleaned)
    except json.JSONDecodeError as e:
        print("ERROR: Found what looked like a JSON array, but it didn't parse correctly.")
        print("Raw response has been saved to raw_output.txt for inspection.")
        with open("raw_output.txt", "w", encoding="utf-8") as f:
            f.write(raw_text)
        raise e


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
    widths = {f: max(len(f), max((len(str(p.get(f, ""))) for p in players), default=0)) for f in fields}

    header = " | ".join(f.ljust(widths[f]) for f in fields)
    print("\n" + header)
    print("-" * len(header))
    for p in players:
        print(" | ".join(str(p.get(f, "")).ljust(widths[f]) for f in fields))
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


async def main():
    if ACTIVE_SPORT_SLUG not in SPORT_PROFILES:
        raise ValueError(
            f"'{ACTIVE_SPORT_SLUG}' is not a known sport profile. "
            f"Choose one of: {list(SPORT_PROFILES.keys())}"
        )
    profile = SPORT_PROFILES[ACTIVE_SPORT_SLUG]
    tier_guidance = TIER_GUIDANCE or profile["default_tier_guidance"]

    print(f"Connecting to local database to load existing {profile['display_name']} roster...")
    existing_names = load_existing_names(ACTIVE_SPORT_SLUG)
    print(f"Loaded {len(existing_names)} existing names to avoid duplicating.")

    all_players = []
    remaining = BATCH_SIZE
    chunk_num = 1

    while remaining > 0:
        this_chunk_size = min(CHUNK_SIZE, remaining)
        print(f"\n--- Chunk {chunk_num}: requesting {this_chunk_size} players ---")

        exclusion_list = existing_names + [p.get("name", "") for p in all_players]
        prompt = build_prompt(profile, this_chunk_size, tier_guidance, exclusion_list)
        raw_response = await call_agent(prompt)

        try:
            chunk_players = parse_players(raw_response)
        except (ValueError, json.JSONDecodeError):
            print(f"Chunk {chunk_num} failed. Stopping here - {len(all_players)}")
            print("players from earlier successful chunks will still be saved.")
            break

        print(f"Chunk {chunk_num} succeeded: {len(chunk_players)} players.")
        all_players.extend(chunk_players)
        remaining -= this_chunk_size
        chunk_num += 1

    if not all_players:
        print("\nNo players were successfully generated. Nothing written.")
        return

    # Always write the CSV log first, regardless of what happens next -
    # this is the permanent record, kept even in the direct-write flow.
    save_players(all_players, OUTPUT_FILE, profile["fields"] + DISPLAY_ONLY_FIELDS)
    print(f"\n{len(all_players)} players researched (logged to {OUTPUT_FILE}).")
    if len(all_players) < BATCH_SIZE:
        print(f"(Requested {BATCH_SIZE}, but a chunk failed partway through - see above.)")

    print_table(all_players, profile["fields"] + DISPLAY_ONLY_FIELDS)

    answer = input(f"Write these {len(all_players)} players to the database? [y/N]: ").strip().lower()
    if answer == "y":
        try:
            write_players_to_db(all_players, ACTIVE_SPORT_SLUG, profile["fields"])
            print(f"\nSuccess: {len(all_players)} players written to the database.")
        except Exception as e:
            print(f"\nERROR: Database write failed, nothing was committed: {e}")
            print(f"The CSV log at {OUTPUT_FILE} still has everything - safe to retry.")
    else:
        print(f"\nSkipped. Nothing written to the database. CSV log saved at {OUTPUT_FILE}.")


if __name__ == "__main__":
    asyncio.run(main())