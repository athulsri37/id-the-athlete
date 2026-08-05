-- Tennis (women's) players, batch 5 (14 players, bringing the roster to 85
-- total: batch 1's 20 + batch 2's 17 + batch 3's 23 + batch 4's 11 + batch
-- 5's 14). Same upsert pattern as prior batches: Players upserts on the
-- unique Name constraint, PlayerAttributeValues upserts on the existing
-- unique (PlayerId, AttributeDefinitionId) constraint, both scoped to the
-- "tennis-women" Sport. No overrides in this batch -- every row is
-- IsOverridden = false, DifficultyOverride = NULL.
--
-- Notes:
-- - Katie Boulter's country is normalized to "United Kingdom" (not "Great
--   Britain"), matching the same normalization already applied to Emma
--   Raducanu (batch 2) and the UK players in the Men's batches.
-- - Alex Eala's country ("Philippines") isn't in the country-closeness
--   adjacency lookup (backend/IdTheAthlete.Api/Geo/CountryProximity.cs) --
--   that lookup degrades gracefully for unknown countries (plain miss,
--   never an error), so no code change is needed here.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Zheng Qinwen',            false, NULL),
    ('Jessica Pegula',          false, NULL),
    ('Paula Badosa',            false, NULL),
    ('Karolína Muchová',        false, NULL),
    ('Sara Errani',             false, NULL),
    ('Emma Navarro',            false, NULL),
    ('Ekaterina Alexandrova',   false, NULL),
    ('Diana Shnaider',          false, NULL),
    ('Magda Linette',           false, NULL),
    ('Beatriz Haddad Maia',     false, NULL),
    ('Katie Boulter',           false, NULL),
    ('Alex Eala',               false, NULL),
    ('Maria Sakkari',           false, NULL),
    ('Linda Noskova',           false, NULL)
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
    -- Player,                       AttrKey,                Value
    ('Zheng Qinwen',                 'active_status',        'Active'),
    ('Zheng Qinwen',                 'plays',                'Right'),
    ('Zheng Qinwen',                 'backhand',             'Two-Handed'),
    ('Zheng Qinwen',                 'country',              'China'),
    ('Zheng Qinwen',                 'grand_slam_titles',    '0'),
    ('Zheng Qinwen',                 'career_high_ranking',  '4'),
    ('Zheng Qinwen',                 'turned_pro_year',      '2018'),
    ('Zheng Qinwen',                 'career_titles',        '5'),

    ('Jessica Pegula',               'active_status',        'Active'),
    ('Jessica Pegula',               'plays',                'Right'),
    ('Jessica Pegula',               'backhand',             'Two-Handed'),
    ('Jessica Pegula',               'country',              'USA'),
    ('Jessica Pegula',               'grand_slam_titles',    '0'),
    ('Jessica Pegula',               'career_high_ranking',  '3'),
    ('Jessica Pegula',               'turned_pro_year',      '2009'),
    ('Jessica Pegula',               'career_titles',        '11'),

    ('Paula Badosa',                 'active_status',        'Active'),
    ('Paula Badosa',                 'plays',                'Right'),
    ('Paula Badosa',                 'backhand',             'Two-Handed'),
    ('Paula Badosa',                 'country',              'Spain'),
    ('Paula Badosa',                 'grand_slam_titles',    '0'),
    ('Paula Badosa',                 'career_high_ranking',  '2'),
    ('Paula Badosa',                 'turned_pro_year',      '2015'),
    ('Paula Badosa',                 'career_titles',        '4'),

    ('Karolína Muchová',             'active_status',        'Active'),
    ('Karolína Muchová',             'plays',                'Right'),
    ('Karolína Muchová',             'backhand',             'Two-Handed'),
    ('Karolína Muchová',             'country',              'Czech Republic'),
    ('Karolína Muchová',             'grand_slam_titles',    '0'),
    ('Karolína Muchová',             'career_high_ranking',  '8'),
    ('Karolína Muchová',             'turned_pro_year',      '2013'),
    ('Karolína Muchová',             'career_titles',        '2'),

    ('Sara Errani',                  'active_status',        'Retired'),
    ('Sara Errani',                  'plays',                'Right'),
    ('Sara Errani',                  'backhand',             'Two-Handed'),
    ('Sara Errani',                  'country',              'Italy'),
    ('Sara Errani',                  'grand_slam_titles',    '0'),
    ('Sara Errani',                  'career_high_ranking',  '5'),
    ('Sara Errani',                  'turned_pro_year',      '2002'),
    ('Sara Errani',                  'career_titles',        '9'),

    ('Emma Navarro',                 'active_status',        'Active'),
    ('Emma Navarro',                 'plays',                'Right'),
    ('Emma Navarro',                 'backhand',             'Two-Handed'),
    ('Emma Navarro',                 'country',              'USA'),
    ('Emma Navarro',                 'grand_slam_titles',    '0'),
    ('Emma Navarro',                 'career_high_ranking',  '8'),
    ('Emma Navarro',                 'turned_pro_year',      '2015'),
    ('Emma Navarro',                 'career_titles',        '3'),

    ('Ekaterina Alexandrova',        'active_status',        'Active'),
    ('Ekaterina Alexandrova',        'plays',                'Right'),
    ('Ekaterina Alexandrova',        'backhand',             'Two-Handed'),
    ('Ekaterina Alexandrova',        'country',              'Russia'),
    ('Ekaterina Alexandrova',        'grand_slam_titles',    '0'),
    ('Ekaterina Alexandrova',        'career_high_ranking',  '10'),
    ('Ekaterina Alexandrova',        'turned_pro_year',      '2011'),
    ('Ekaterina Alexandrova',        'career_titles',        '5'),

    ('Diana Shnaider',               'active_status',        'Active'),
    ('Diana Shnaider',               'plays',                'Left'),
    ('Diana Shnaider',               'backhand',             'Two-Handed'),
    ('Diana Shnaider',               'country',              'Russia'),
    ('Diana Shnaider',               'grand_slam_titles',    '0'),
    ('Diana Shnaider',               'career_high_ranking',  '11'),
    ('Diana Shnaider',               'turned_pro_year',      '2023'),
    ('Diana Shnaider',               'career_titles',        '5'),

    ('Magda Linette',                'active_status',        'Active'),
    ('Magda Linette',                'plays',                'Right'),
    ('Magda Linette',                'backhand',             'Two-Handed'),
    ('Magda Linette',                'country',              'Poland'),
    ('Magda Linette',                'grand_slam_titles',    '0'),
    ('Magda Linette',                'career_high_ranking',  '19'),
    ('Magda Linette',                'turned_pro_year',      '2009'),
    ('Magda Linette',                'career_titles',        '3'),

    ('Beatriz Haddad Maia',          'active_status',        'Active'),
    ('Beatriz Haddad Maia',          'plays',                'Left'),
    ('Beatriz Haddad Maia',          'backhand',             'Two-Handed'),
    ('Beatriz Haddad Maia',          'country',              'Brazil'),
    ('Beatriz Haddad Maia',          'grand_slam_titles',    '0'),
    ('Beatriz Haddad Maia',          'career_high_ranking',  '10'),
    ('Beatriz Haddad Maia',          'turned_pro_year',      '2014'),
    ('Beatriz Haddad Maia',          'career_titles',        '4'),

    ('Katie Boulter',                'active_status',        'Active'),
    ('Katie Boulter',                'plays',                'Right'),
    ('Katie Boulter',                'backhand',             'Two-Handed'),
    ('Katie Boulter',                'country',              'United Kingdom'),
    ('Katie Boulter',                'grand_slam_titles',    '0'),
    ('Katie Boulter',                'career_high_ranking',  '23'),
    ('Katie Boulter',                'turned_pro_year',      '2011'),
    ('Katie Boulter',                'career_titles',        '4'),

    ('Alex Eala',                    'active_status',        'Active'),
    ('Alex Eala',                    'plays',                'Left'),
    ('Alex Eala',                    'backhand',             'Two-Handed'),
    ('Alex Eala',                    'country',              'Philippines'),
    ('Alex Eala',                    'grand_slam_titles',    '0'),
    ('Alex Eala',                    'career_high_ranking',  '29'),
    ('Alex Eala',                    'turned_pro_year',      '2020'),
    ('Alex Eala',                    'career_titles',        '0'),

    ('Maria Sakkari',                'active_status',        'Active'),
    ('Maria Sakkari',                'plays',                'Right'),
    ('Maria Sakkari',                'backhand',             'Two-Handed'),
    ('Maria Sakkari',                'country',              'Greece'),
    ('Maria Sakkari',                'grand_slam_titles',    '0'),
    ('Maria Sakkari',                'career_high_ranking',  '3'),
    ('Maria Sakkari',                'turned_pro_year',      '2015'),
    ('Maria Sakkari',                'career_titles',        '2'),

    ('Linda Noskova',                'active_status',        'Active'),
    ('Linda Noskova',                'plays',                'Right'),
    ('Linda Noskova',                'backhand',             'Two-Handed'),
    ('Linda Noskova',                'country',              'Czech Republic'),
    ('Linda Noskova',                'grand_slam_titles',    '0'),
    ('Linda Noskova',                'career_high_ranking',  '12'),
    ('Linda Noskova',                'turned_pro_year',      '2019'),
    ('Linda Noskova',                'career_titles',        '1')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";