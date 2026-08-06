-- Men's International Cricket sport row and its attribute definitions.
-- Safe to re-run: both upserts are keyed on unique constraints
-- (Sports.Slug, AttributeDefinitions(SportId, Key)) added in migration
-- AddPlayerNameAndAttributeDefinitionUniqueConstraints.
--
-- Sibling to Cricket/WomenInternational/00-sport-and-attributes.sql: a
-- separate Sports row (its own Id), scoped to its own AttributeDefinitions
-- via WHERE s."Slug" = 'cricket-men-international' below -- never shared
-- with or duplicated from Women's International Cricket, the same
-- isolation pattern already used for Men's/Women's Tennis. Player data is
-- deliberately not seeded yet; player-batch files land in a follow-up once
-- the roster is curated and verified.

INSERT INTO "Sports" ("Name", "Slug")
VALUES ('Men''s International Cricket', 'cricket-men-international')
ON CONFLICT ("Slug") DO UPDATE SET
    "Name" = EXCLUDED."Name";

-- One-time rename: "bowling_hand" (Right/Left/Ambidextrous/Doesn't Bowl) is
-- being replaced by "bowling_style", which distinguishes pace vs. spin
-- rather than just arm used. No player data has been seeded against this
-- sport yet, so this is a straight rename of the AttributeDefinition row
-- (preserving its Id) rather than a data migration. Idempotent: a re-run
-- after the rename has already happened finds no "bowling_hand" row left
-- and is a no-op.
UPDATE "AttributeDefinitions" ad
SET "Key" = 'bowling_style'
FROM "Sports" s
WHERE ad."SportId" = s."Id"
  AND s."Slug" = 'cricket-men-international'
  AND ad."Key" = 'bowling_hand';

INSERT INTO "AttributeDefinitions" ("SportId", "Key", "Label", "Type", "DisplayOrder")
SELECT s."Id", v."Key", v."Label", v."Type", v."DisplayOrder"
FROM "Sports" s
CROSS JOIN (VALUES
    -- Key,                   Label,             Type (0=Categorical, 1=Numeric), DisplayOrder
    -- bowling_style values: Right-arm Pace / Right-arm Spin / Left-arm Pace /
    --   Left-arm Spin / Hasn't Bowled
    -- role values: Batter / Bowler / All-rounder / Batting All-rounder /
    --   Bowling All-rounder / Wicketkeeper-Batter
    ('active_status',         'Status',          0, 0),
    ('country',               'Country',         0, 1),
    ('batting_hand',          'Batting Hand',    0, 2),
    ('bowling_style',         'Bowling Style',   0, 3),
    ('role',                  'Role',            0, 4),
    ('combined_matches',      'Matches',         1, 5),
    ('combined_runs',         'Runs',            1, 6),
    ('combined_wickets',      'Wickets',         1, 7),
    ('debut_year',            'Debut Year',      1, 8)
) AS v("Key", "Label", "Type", "DisplayOrder")
WHERE s."Slug" = 'cricket-men-international'
ON CONFLICT ("SportId", "Key") DO UPDATE SET
    "Label" = EXCLUDED."Label",
    "Type" = EXCLUDED."Type",
    "DisplayOrder" = EXCLUDED."DisplayOrder";
