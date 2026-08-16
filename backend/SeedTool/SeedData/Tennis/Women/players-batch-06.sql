-- Tennis (women's) players, batch 6 (11 players, bringing the roster to 96
-- total: batch 1's 20 + batch 2's 17 + batch 3's 23 + batch 4's 11 + batch
-- 5's 14 + batch 6's 11). Same upsert pattern as prior batches: Players
-- upserts on the unique Name constraint, PlayerAttributeValues upserts on
-- the existing unique (PlayerId, AttributeDefinitionId) constraint, both
-- scoped to the "tennis-women" Sport. No overrides in this batch -- every
-- row is IsOverridden = false, DifficultyOverride = NULL.
--
-- Note: Johanna Konta's country is normalized to "United Kingdom" (not
-- "Great Britain"), matching the same normalization already applied to
-- Emma Raducanu (batch 2), Katie Boulter (batch 5), and the UK players in
-- the Men's batches.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Agnieszka Radwańska',     false, NULL),
    ('Sabine Lisicki',          false, NULL),
    ('Dominika Cibulková',      false, NULL),
    ('Johanna Konta',           false, NULL),
    ('Anett Kontaveit',         false, NULL),
    ('Eugenie Bouchard',        false, NULL),
    ('Andrea Petkovic',         false, NULL),
    ('Alizé Cornet',            false, NULL),
    ('Camila Giorgi',           false, NULL),
    ('Carla Suárez Navarro',    false, NULL),
    ('Anastasija Sevastova',    false, NULL)
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
    ('Agnieszka Radwańska',          'active_status',        'Retired'),
    ('Agnieszka Radwańska',          'plays',                'Right'),
    ('Agnieszka Radwańska',          'backhand',             'Two-Handed'),
    ('Agnieszka Radwańska',          'country',              'Poland'),
    ('Agnieszka Radwańska',          'grand_slam_titles',    '0'),
    ('Agnieszka Radwańska',          'career_high_ranking',  '2'),
    ('Agnieszka Radwańska',          'turned_pro_year',      '2005'),
    ('Agnieszka Radwańska',          'career_titles',        '20'),

    ('Sabine Lisicki',               'active_status',        'Retired'),
    ('Sabine Lisicki',               'plays',                'Right'),
    ('Sabine Lisicki',               'backhand',             'Two-Handed'),
    ('Sabine Lisicki',               'country',              'Germany'),
    ('Sabine Lisicki',               'grand_slam_titles',    '0'),
    ('Sabine Lisicki',               'career_high_ranking',  '12'),
    ('Sabine Lisicki',               'turned_pro_year',      '2006'),
    ('Sabine Lisicki',               'career_titles',        '4'),

    ('Dominika Cibulková',           'active_status',        'Retired'),
    ('Dominika Cibulková',           'plays',                'Right'),
    ('Dominika Cibulková',           'backhand',             'Two-Handed'),
    ('Dominika Cibulková',           'country',              'Slovakia'),
    ('Dominika Cibulková',           'grand_slam_titles',    '0'),
    ('Dominika Cibulková',           'career_high_ranking',  '4'),
    ('Dominika Cibulková',           'turned_pro_year',      '2004'),
    ('Dominika Cibulková',           'career_titles',        '8'),

    ('Johanna Konta',                'active_status',        'Retired'),
    ('Johanna Konta',                'plays',                'Right'),
    ('Johanna Konta',                'backhand',             'Two-Handed'),
    ('Johanna Konta',                'country',              'United Kingdom'),
    ('Johanna Konta',                'grand_slam_titles',    '0'),
    ('Johanna Konta',                'career_high_ranking',  '4'),
    ('Johanna Konta',                'turned_pro_year',      '2008'),
    ('Johanna Konta',                'career_titles',        '4'),

    ('Anett Kontaveit',              'active_status',        'Retired'),
    ('Anett Kontaveit',              'plays',                'Right'),
    ('Anett Kontaveit',              'backhand',             'Two-Handed'),
    ('Anett Kontaveit',              'country',              'Estonia'),
    ('Anett Kontaveit',              'grand_slam_titles',    '0'),
    ('Anett Kontaveit',              'career_high_ranking',  '2'),
    ('Anett Kontaveit',              'turned_pro_year',      '2010'),
    ('Anett Kontaveit',              'career_titles',        '6'),

    ('Eugenie Bouchard',             'active_status',        'Retired'),
    ('Eugenie Bouchard',             'plays',                'Right'),
    ('Eugenie Bouchard',             'backhand',             'Two-Handed'),
    ('Eugenie Bouchard',             'country',              'Canada'),
    ('Eugenie Bouchard',             'grand_slam_titles',    '0'),
    ('Eugenie Bouchard',             'career_high_ranking',  '5'),
    ('Eugenie Bouchard',             'turned_pro_year',      '2009'),
    ('Eugenie Bouchard',             'career_titles',        '1'),

    ('Andrea Petkovic',              'active_status',        'Retired'),
    ('Andrea Petkovic',              'plays',                'Right'),
    ('Andrea Petkovic',              'backhand',             'Two-Handed'),
    ('Andrea Petkovic',              'country',              'Germany'),
    ('Andrea Petkovic',              'grand_slam_titles',    '0'),
    ('Andrea Petkovic',              'career_high_ranking',  '9'),
    ('Andrea Petkovic',              'turned_pro_year',      '2006'),
    ('Andrea Petkovic',              'career_titles',        '7'),

    ('Alizé Cornet',                 'active_status',        'Retired'),
    ('Alizé Cornet',                 'plays',                'Right'),
    ('Alizé Cornet',                 'backhand',             'Two-Handed'),
    ('Alizé Cornet',                 'country',              'France'),
    ('Alizé Cornet',                 'grand_slam_titles',    '0'),
    ('Alizé Cornet',                 'career_high_ranking',  '11'),
    ('Alizé Cornet',                 'turned_pro_year',      '2006'),
    ('Alizé Cornet',                 'career_titles',        '6'),

    ('Camila Giorgi',                'active_status',        'Retired'),
    ('Camila Giorgi',                'plays',                'Right'),
    ('Camila Giorgi',                'backhand',             'Two-Handed'),
    ('Camila Giorgi',                'country',              'Italy'),
    ('Camila Giorgi',                'grand_slam_titles',    '0'),
    ('Camila Giorgi',                'career_high_ranking',  '26'),
    ('Camila Giorgi',                'turned_pro_year',      '2006'),
    ('Camila Giorgi',                'career_titles',        '4'),

    ('Carla Suárez Navarro',         'active_status',        'Retired'),
    ('Carla Suárez Navarro',         'plays',                'Right'),
    ('Carla Suárez Navarro',         'backhand',             'One-Handed'),
    ('Carla Suárez Navarro',         'country',              'Spain'),
    ('Carla Suárez Navarro',         'grand_slam_titles',    '0'),
    ('Carla Suárez Navarro',         'career_high_ranking',  '6'),
    ('Carla Suárez Navarro',         'turned_pro_year',      '2003'),
    ('Carla Suárez Navarro',         'career_titles',        '2'),

    ('Anastasija Sevastova',         'active_status',        'Retired'),
    ('Anastasija Sevastova',         'plays',                'Right'),
    ('Anastasija Sevastova',         'backhand',             'Two-Handed'),
    ('Anastasija Sevastova',         'country',              'Latvia'),
    ('Anastasija Sevastova',         'grand_slam_titles',    '0'),
    ('Anastasija Sevastova',         'career_high_ranking',  '11'),
    ('Anastasija Sevastova',         'turned_pro_year',      '2006'),
    ('Anastasija Sevastova',         'career_titles',        '4')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value"
WHERE "PlayerAttributeValues"."IsManuallyEdited" = false;