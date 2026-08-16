-- Tennis (women's) players, batch 2 (17 players, bringing the roster to 37
-- total: batch 1's 20 + batch 2's 17). Same upsert pattern as prior
-- batches: Players upserts on the unique Name constraint,
-- PlayerAttributeValues upserts on the existing unique (PlayerId,
-- AttributeDefinitionId) constraint, both scoped to the "tennis-women"
-- Sport. No overrides in this batch -- every row is IsOverridden = false,
-- DifficultyOverride = NULL.
--
-- Note: Emma Raducanu's country is normalized to "United Kingdom" (not
-- "Great Britain"), matching the same normalization already applied to UK
-- players in the Men's batches, to keep a single consistent value for this
-- country.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Suzanne Lenglen',          false, NULL),
    ('Tracy Austin',             false, NULL),
    ('Amélie Mauresmo',          false, NULL),
    ('Ana Ivanovic',             false, NULL),
    ('Jennifer Capriati',        false, NULL),
    ('Arantxa Sánchez Vicario',  false, NULL),
    ('Petra Kvitová',            false, NULL),
    ('Angelique Kerber',         false, NULL),
    ('Simona Halep',             false, NULL),
    ('Bianca Andreescu',         false, NULL),
    ('Sloane Stephens',          false, NULL),
    ('Garbiñe Muguruza',         false, NULL),
    ('Elena Rybakina',           false, NULL),
    ('Emma Raducanu',            false, NULL),
    ('Ashleigh Barty',           false, NULL),
    ('Svetlana Kuznetsova',      false, NULL),
    ('Jeļena Ostapenko',         false, NULL)
) AS v("Name", "IsOverridden", "DifficultyOverride")
WHERE s."Slug" = 'tennis-women'
ON CONFLICT ("Name") DO UPDATE SET
    "SportId" = EXCLUDED."SportId",
    "IsOverridden" = CASE WHEN "Players"."IsOverridden" THEN "Players"."IsOverridden" ELSE EXCLUDED."IsOverridden" END,
    "DifficultyOverride" = CASE WHEN "Players"."IsOverridden" THEN "Players"."DifficultyOverride" ELSE EXCLUDED."DifficultyOverride" END;

-- One row per (player, attribute) pair. PlayerId/AttributeDefinitionId are
-- resolved by name/key lookup (scoped to each player's own SportId), so
-- this file has no dependency on insertion order or id values, and can
-- never cross-attach to tennis-men's AttributeDefinitions.
INSERT INTO "PlayerAttributeValues" ("PlayerId", "AttributeDefinitionId", "Value")
SELECT p."Id", ad."Id", v."Value"
FROM (VALUES
    -- Player,                       AttrKey,                Value
    ('Suzanne Lenglen',              'active_status',        'Retired'),
    ('Suzanne Lenglen',              'plays',                'Right'),
    ('Suzanne Lenglen',              'backhand',             'One-Handed'),
    ('Suzanne Lenglen',              'country',              'France'),
    ('Suzanne Lenglen',              'grand_slam_titles',    '8'),
    ('Suzanne Lenglen',              'career_high_ranking',  '1'),
    ('Suzanne Lenglen',              'turned_pro_year',      '1926'),
    ('Suzanne Lenglen',              'career_titles',        '83'),

    ('Tracy Austin',                 'active_status',        'Retired'),
    ('Tracy Austin',                 'plays',                'Right'),
    ('Tracy Austin',                 'backhand',             'Two-Handed'),
    ('Tracy Austin',                 'country',              'USA'),
    ('Tracy Austin',                 'grand_slam_titles',    '2'),
    ('Tracy Austin',                 'career_high_ranking',  '1'),
    ('Tracy Austin',                 'turned_pro_year',      '1978'),
    ('Tracy Austin',                 'career_titles',        '30'),

    ('Amélie Mauresmo',              'active_status',        'Retired'),
    ('Amélie Mauresmo',              'plays',                'Right'),
    ('Amélie Mauresmo',              'backhand',             'One-Handed'),
    ('Amélie Mauresmo',              'country',              'France'),
    ('Amélie Mauresmo',              'grand_slam_titles',    '2'),
    ('Amélie Mauresmo',              'career_high_ranking',  '1'),
    ('Amélie Mauresmo',              'turned_pro_year',      '1993'),
    ('Amélie Mauresmo',              'career_titles',        '25'),

    ('Ana Ivanovic',                 'active_status',        'Retired'),
    ('Ana Ivanovic',                 'plays',                'Right'),
    ('Ana Ivanovic',                 'backhand',             'Two-Handed'),
    ('Ana Ivanovic',                 'country',              'Serbia'),
    ('Ana Ivanovic',                 'grand_slam_titles',    '1'),
    ('Ana Ivanovic',                 'career_high_ranking',  '1'),
    ('Ana Ivanovic',                 'turned_pro_year',      '2003'),
    ('Ana Ivanovic',                 'career_titles',        '15'),

    ('Jennifer Capriati',            'active_status',        'Retired'),
    ('Jennifer Capriati',            'plays',                'Right'),
    ('Jennifer Capriati',            'backhand',             'Two-Handed'),
    ('Jennifer Capriati',            'country',              'USA'),
    ('Jennifer Capriati',            'grand_slam_titles',    '3'),
    ('Jennifer Capriati',            'career_high_ranking',  '1'),
    ('Jennifer Capriati',            'turned_pro_year',      '1990'),
    ('Jennifer Capriati',            'career_titles',        '14'),

    ('Arantxa Sánchez Vicario',      'active_status',        'Retired'),
    ('Arantxa Sánchez Vicario',      'plays',                'Right'),
    ('Arantxa Sánchez Vicario',      'backhand',             'Two-Handed'),
    ('Arantxa Sánchez Vicario',      'country',              'Spain'),
    ('Arantxa Sánchez Vicario',      'grand_slam_titles',    '4'),
    ('Arantxa Sánchez Vicario',      'career_high_ranking',  '1'),
    ('Arantxa Sánchez Vicario',      'turned_pro_year',      '1985'),
    ('Arantxa Sánchez Vicario',      'career_titles',        '29'),

    ('Petra Kvitová',                'active_status',        'Retired'),
    ('Petra Kvitová',                'plays',                'Left'),
    ('Petra Kvitová',                'backhand',             'Two-Handed'),
    ('Petra Kvitová',                'country',              'Czech Republic'),
    ('Petra Kvitová',                'grand_slam_titles',    '2'),
    ('Petra Kvitová',                'career_high_ranking',  '2'),
    ('Petra Kvitová',                'turned_pro_year',      '2006'),
    ('Petra Kvitová',                'career_titles',        '31'),

    ('Angelique Kerber',             'active_status',        'Retired'),
    ('Angelique Kerber',             'plays',                'Left'),
    ('Angelique Kerber',             'backhand',             'Two-Handed'),
    ('Angelique Kerber',             'country',              'Germany'),
    ('Angelique Kerber',             'grand_slam_titles',    '3'),
    ('Angelique Kerber',             'career_high_ranking',  '1'),
    ('Angelique Kerber',             'turned_pro_year',      '2003'),
    ('Angelique Kerber',             'career_titles',        '14'),

    ('Simona Halep',                 'active_status',        'Retired'),
    ('Simona Halep',                 'plays',                'Right'),
    ('Simona Halep',                 'backhand',             'Two-Handed'),
    ('Simona Halep',                 'country',              'Romania'),
    ('Simona Halep',                 'grand_slam_titles',    '2'),
    ('Simona Halep',                 'career_high_ranking',  '1'),
    ('Simona Halep',                 'turned_pro_year',      '2006'),
    ('Simona Halep',                 'career_titles',        '24'),

    ('Bianca Andreescu',             'active_status',        'Active'),
    ('Bianca Andreescu',             'plays',                'Right'),
    ('Bianca Andreescu',             'backhand',             'Two-Handed'),
    ('Bianca Andreescu',             'country',              'Canada'),
    ('Bianca Andreescu',             'grand_slam_titles',    '1'),
    ('Bianca Andreescu',             'career_high_ranking',  '4'),
    ('Bianca Andreescu',             'turned_pro_year',      '2017'),
    ('Bianca Andreescu',             'career_titles',        '3'),

    ('Sloane Stephens',              'active_status',        'Active'),
    ('Sloane Stephens',              'plays',                'Right'),
    ('Sloane Stephens',              'backhand',             'Two-Handed'),
    ('Sloane Stephens',              'country',              'USA'),
    ('Sloane Stephens',              'grand_slam_titles',    '1'),
    ('Sloane Stephens',              'career_high_ranking',  '3'),
    ('Sloane Stephens',              'turned_pro_year',      '2009'),
    ('Sloane Stephens',              'career_titles',        '8'),

    ('Garbiñe Muguruza',             'active_status',        'Retired'),
    ('Garbiñe Muguruza',             'plays',                'Right'),
    ('Garbiñe Muguruza',             'backhand',             'Two-Handed'),
    ('Garbiñe Muguruza',             'country',              'Spain'),
    ('Garbiñe Muguruza',             'grand_slam_titles',    '2'),
    ('Garbiñe Muguruza',             'career_high_ranking',  '1'),
    ('Garbiñe Muguruza',             'turned_pro_year',      '2012'),
    ('Garbiñe Muguruza',             'career_titles',        '10'),

    ('Elena Rybakina',               'active_status',        'Active'),
    ('Elena Rybakina',               'plays',                'Right'),
    ('Elena Rybakina',               'backhand',             'Two-Handed'),
    ('Elena Rybakina',               'country',              'Kazakhstan'),
    ('Elena Rybakina',               'grand_slam_titles',    '2'),
    ('Elena Rybakina',               'career_high_ranking',  '2'),
    ('Elena Rybakina',               'turned_pro_year',      '2014'),
    ('Elena Rybakina',               'career_titles',        '13'),

    ('Emma Raducanu',                'active_status',        'Active'),
    ('Emma Raducanu',                'plays',                'Right'),
    ('Emma Raducanu',                'backhand',             'Two-Handed'),
    ('Emma Raducanu',                'country',              'United Kingdom'),
    ('Emma Raducanu',                'grand_slam_titles',    '1'),
    ('Emma Raducanu',                'career_high_ranking',  '10'),
    ('Emma Raducanu',                'turned_pro_year',      '2018'),
    ('Emma Raducanu',                'career_titles',        '1'),

    ('Ashleigh Barty',                'active_status',        'Retired'),
    ('Ashleigh Barty',                'plays',                'Right'),
    ('Ashleigh Barty',                'backhand',             'Two-Handed'),
    ('Ashleigh Barty',                'country',              'Australia'),
    ('Ashleigh Barty',                'grand_slam_titles',    '3'),
    ('Ashleigh Barty',                'career_high_ranking',  '1'),
    ('Ashleigh Barty',                'turned_pro_year',      '2010'),
    ('Ashleigh Barty',                'career_titles',        '15'),

    ('Svetlana Kuznetsova',           'active_status',        'Retired'),
    ('Svetlana Kuznetsova',           'plays',                'Right'),
    ('Svetlana Kuznetsova',           'backhand',             'Two-Handed'),
    ('Svetlana Kuznetsova',           'country',              'Russia'),
    ('Svetlana Kuznetsova',           'grand_slam_titles',    '2'),
    ('Svetlana Kuznetsova',           'career_high_ranking',  '2'),
    ('Svetlana Kuznetsova',           'turned_pro_year',      '2000'),
    ('Svetlana Kuznetsova',           'career_titles',        '18'),

    ('Jeļena Ostapenko',              'active_status',        'Active'),
    ('Jeļena Ostapenko',              'plays',                'Right'),
    ('Jeļena Ostapenko',              'backhand',             'Two-Handed'),
    ('Jeļena Ostapenko',              'country',              'Latvia'),
    ('Jeļena Ostapenko',              'grand_slam_titles',    '1'),
    ('Jeļena Ostapenko',              'career_high_ranking',  '5'),
    ('Jeļena Ostapenko',              'turned_pro_year',      '2012'),
    ('Jeļena Ostapenko',              'career_titles',        '9')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value"
WHERE "PlayerAttributeValues"."IsManuallyEdited" = false;