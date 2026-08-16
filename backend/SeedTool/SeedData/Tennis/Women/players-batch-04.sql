-- Tennis (women's) players, batch 4 (11 players, bringing the roster to 71
-- total: batch 1's 20 + batch 2's 17 + batch 3's 23 + batch 4's 11). Same
-- upsert pattern as prior batches: Players upserts on the unique Name
-- constraint, PlayerAttributeValues upserts on the existing unique
-- (PlayerId, AttributeDefinitionId) constraint, both scoped to the
-- "tennis-women" Sport. No overrides in this batch -- every row is
-- IsOverridden = false, DifficultyOverride = NULL.
--
-- Note: Monica Puig's country ("Puerto Rico") isn't in the country-closeness
-- adjacency lookup (backend/IdTheAthlete.Api/Geo/CountryProximity.cs) --
-- that lookup degrades gracefully for unknown countries (treated as a
-- plain miss, never an error), so this doesn't need a code change here;
-- flagging only in case the roster expands enough around this country to
-- warrant adding it later.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Danielle Collins',       false, NULL),
    ('Anna Kournikova',        false, NULL),
    ('Conchita Martínez',      false, NULL),
    ('Iva Majoli',             false, NULL),
    ('Mary Joe Fernández',     false, NULL),
    ('Jana Novotná',           false, NULL),
    ('Hana Mandlíková',        false, NULL),
    ('Monica Puig',            false, NULL),
    ('Leylah Fernández',       false, NULL),
    ('Marta Kostyuk',          false, NULL),
    ('Elena Dementieva',       false, NULL)
) AS v("Name", "IsOverridden", "DifficultyOverride")
WHERE s."Slug" = 'tennis-women'
ON CONFLICT ("Name") DO UPDATE SET
    "SportId" = EXCLUDED."SportId",
    "IsOverridden" = EXCLUDED."IsOverridden",
    "DifficultyOverride" = EXCLUDED."DifficultyOverride";

-- One row per (player, attribute) pair. PlayerId/AttributeDefinitionId are
-- resolved by name/key lookup (scoped to each player's own SportId), so
-- this file has no dependency on insertion order or id values, and can
-- never cross-attach to tennis-men's AttributeDefinitions.
INSERT INTO "PlayerAttributeValues" ("PlayerId", "AttributeDefinitionId", "Value")
SELECT p."Id", ad."Id", v."Value"
FROM (VALUES
    -- Player,                    AttrKey,                Value
    ('Danielle Collins',          'active_status',        'Active'),
    ('Danielle Collins',          'plays',                'Right'),
    ('Danielle Collins',          'backhand',             'Two-Handed'),
    ('Danielle Collins',          'country',              'USA'),
    ('Danielle Collins',          'grand_slam_titles',    '0'),
    ('Danielle Collins',          'career_high_ranking',  '7'),
    ('Danielle Collins',          'turned_pro_year',      '2016'),
    ('Danielle Collins',          'career_titles',        '4'),

    ('Anna Kournikova',           'active_status',        'Retired'),
    ('Anna Kournikova',           'plays',                'Right'),
    ('Anna Kournikova',           'backhand',             'Two-Handed'),
    ('Anna Kournikova',           'country',              'Russia'),
    ('Anna Kournikova',           'grand_slam_titles',    '0'),
    ('Anna Kournikova',           'career_high_ranking',  '8'),
    ('Anna Kournikova',           'turned_pro_year',      '1995'),
    ('Anna Kournikova',           'career_titles',        '0'),

    ('Conchita Martínez',         'active_status',        'Retired'),
    ('Conchita Martínez',         'plays',                'Right'),
    ('Conchita Martínez',         'backhand',             'One-Handed'),
    ('Conchita Martínez',         'country',              'Spain'),
    ('Conchita Martínez',         'grand_slam_titles',    '1'),
    ('Conchita Martínez',         'career_high_ranking',  '2'),
    ('Conchita Martínez',         'turned_pro_year',      '1988'),
    ('Conchita Martínez',         'career_titles',        '33'),

    ('Iva Majoli',                'active_status',        'Retired'),
    ('Iva Majoli',                'plays',                'Right'),
    ('Iva Majoli',                'backhand',             'Two-Handed'),
    ('Iva Majoli',                'country',              'Croatia'),
    ('Iva Majoli',                'grand_slam_titles',    '1'),
    ('Iva Majoli',                'career_high_ranking',  '4'),
    ('Iva Majoli',                'turned_pro_year',      '1991'),
    ('Iva Majoli',                'career_titles',        '8'),

    ('Mary Joe Fernández',        'active_status',        'Retired'),
    ('Mary Joe Fernández',        'plays',                'Right'),
    ('Mary Joe Fernández',        'backhand',             'Two-Handed'),
    ('Mary Joe Fernández',        'country',              'USA'),
    ('Mary Joe Fernández',        'grand_slam_titles',    '0'),
    ('Mary Joe Fernández',        'career_high_ranking',  '4'),
    ('Mary Joe Fernández',        'turned_pro_year',      '1986'),
    ('Mary Joe Fernández',        'career_titles',        '7'),

    ('Jana Novotná',              'active_status',        'Retired'),
    ('Jana Novotná',              'plays',                'Right'),
    ('Jana Novotná',              'backhand',             'One-Handed'),
    ('Jana Novotná',              'country',              'Czech Republic'),
    ('Jana Novotná',              'grand_slam_titles',    '1'),
    ('Jana Novotná',              'career_high_ranking',  '2'),
    ('Jana Novotná',              'turned_pro_year',      '1987'),
    ('Jana Novotná',              'career_titles',        '24'),

    ('Hana Mandlíková',           'active_status',        'Retired'),
    ('Hana Mandlíková',           'plays',                'Right'),
    ('Hana Mandlíková',           'backhand',             'One-Handed'),
    ('Hana Mandlíková',           'country',              'Australia'),
    ('Hana Mandlíková',           'grand_slam_titles',    '4'),
    ('Hana Mandlíková',           'career_high_ranking',  '3'),
    ('Hana Mandlíková',           'turned_pro_year',      '1978'),
    ('Hana Mandlíková',           'career_titles',        '27'),

    ('Monica Puig',               'active_status',        'Retired'),
    ('Monica Puig',               'plays',                'Right'),
    ('Monica Puig',               'backhand',             'Two-Handed'),
    ('Monica Puig',               'country',              'Puerto Rico'),
    ('Monica Puig',               'grand_slam_titles',    '0'),
    ('Monica Puig',               'career_high_ranking',  '27'),
    ('Monica Puig',               'turned_pro_year',      '2010'),
    ('Monica Puig',               'career_titles',        '2'),

    ('Leylah Fernández',          'active_status',        'Active'),
    ('Leylah Fernández',          'plays',                'Left'),
    ('Leylah Fernández',          'backhand',             'Two-Handed'),
    ('Leylah Fernández',          'country',              'Canada'),
    ('Leylah Fernández',          'grand_slam_titles',    '0'),
    ('Leylah Fernández',          'career_high_ranking',  '13'),
    ('Leylah Fernández',          'turned_pro_year',      '2019'),
    ('Leylah Fernández',          'career_titles',        '5'),

    ('Marta Kostyuk',             'active_status',        'Active'),
    ('Marta Kostyuk',             'plays',                'Right'),
    ('Marta Kostyuk',             'backhand',             'Two-Handed'),
    ('Marta Kostyuk',             'country',              'Ukraine'),
    ('Marta Kostyuk',             'grand_slam_titles',    '0'),
    ('Marta Kostyuk',             'career_high_ranking',  '13'),
    ('Marta Kostyuk',             'turned_pro_year',      '2016'),
    ('Marta Kostyuk',             'career_titles',        '3'),

    ('Elena Dementieva',          'active_status',        'Retired'),
    ('Elena Dementieva',          'plays',                'Right'),
    ('Elena Dementieva',          'backhand',             'Two-Handed'),
    ('Elena Dementieva',          'country',              'Russia'),
    ('Elena Dementieva',          'grand_slam_titles',    '0'),
    ('Elena Dementieva',          'career_high_ranking',  '3'),
    ('Elena Dementieva',          'turned_pro_year',      '1998'),
    ('Elena Dementieva',          'career_titles',        '16')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value"
WHERE "PlayerAttributeValues"."IsManuallyEdited" = false;