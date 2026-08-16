-- Tennis (women's) players, batch 1 (20 players, first batch of the WTA
-- roster). Same upsert pattern as the Men's batch files: Players upserts
-- on the unique Name constraint, PlayerAttributeValues upserts on the
-- existing unique (PlayerId, AttributeDefinitionId) constraint. Scoped to
-- the "tennis-women" Sport throughout -- the Players insert resolves
-- SportId via WHERE s."Slug" = 'tennis-women' below, and the
-- PlayerAttributeValues join matches AttributeDefinitions on that same
-- resolved SportId, so this never touches tennis-men's players or
-- attribute definitions. No overrides in this batch -- every row is
-- IsOverridden = false, DifficultyOverride = NULL.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Serena Williams',      false, NULL),
    ('Venus Williams',       false, NULL),
    ('Steffi Graf',          false, NULL),
    ('Martina Navratilova',  false, NULL),
    ('Chris Evert',          false, NULL),
    ('Billie Jean King',     false, NULL),
    ('Maria Sharapova',      false, NULL),
    ('Naomi Osaka',          false, NULL),
    ('Monica Seles',         false, NULL),
    ('Martina Hingis',       false, NULL),
    ('Justine Henin',        false, NULL),
    ('Kim Clijsters',        false, NULL),
    ('Lindsay Davenport',    false, NULL),
    ('Victoria Azarenka',    false, NULL),
    ('Caroline Wozniacki',   false, NULL),
    ('Coco Gauff',           false, NULL),
    ('Iga Świątek',          false, NULL),
    ('Aryna Sabalenka',      false, NULL),
    ('Margaret Court',       false, NULL),
    ('Li Na',                false, NULL)
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
    -- Player,                  AttrKey,                Value
    ('Serena Williams',         'active_status',        'Retired'),
    ('Serena Williams',         'plays',                'Right'),
    ('Serena Williams',         'backhand',             'Two-Handed'),
    ('Serena Williams',         'country',              'USA'),
    ('Serena Williams',         'grand_slam_titles',    '23'),
    ('Serena Williams',         'career_high_ranking',  '1'),
    ('Serena Williams',         'turned_pro_year',      '1995'),
    ('Serena Williams',         'career_titles',        '73'),

    ('Venus Williams',          'active_status',        'Active'),
    ('Venus Williams',          'plays',                'Right'),
    ('Venus Williams',          'backhand',             'Two-Handed'),
    ('Venus Williams',          'country',              'USA'),
    ('Venus Williams',          'grand_slam_titles',    '7'),
    ('Venus Williams',          'career_high_ranking',  '1'),
    ('Venus Williams',          'turned_pro_year',      '1994'),
    ('Venus Williams',          'career_titles',        '49'),

    ('Steffi Graf',             'active_status',        'Retired'),
    ('Steffi Graf',             'plays',                'Right'),
    ('Steffi Graf',             'backhand',             'One-Handed'),
    ('Steffi Graf',             'country',              'Germany'),
    ('Steffi Graf',             'grand_slam_titles',    '22'),
    ('Steffi Graf',             'career_high_ranking',  '1'),
    ('Steffi Graf',             'turned_pro_year',      '1982'),
    ('Steffi Graf',             'career_titles',        '107'),

    ('Martina Navratilova',     'active_status',        'Retired'),
    ('Martina Navratilova',     'plays',                'Left'),
    ('Martina Navratilova',     'backhand',             'One-Handed'),
    ('Martina Navratilova',     'country',              'USA'),
    ('Martina Navratilova',     'grand_slam_titles',    '18'),
    ('Martina Navratilova',     'career_high_ranking',  '1'),
    ('Martina Navratilova',     'turned_pro_year',      '1974'),
    ('Martina Navratilova',     'career_titles',        '167'),

    ('Chris Evert',             'active_status',        'Retired'),
    ('Chris Evert',             'plays',                'Right'),
    ('Chris Evert',             'backhand',             'Two-Handed'),
    ('Chris Evert',             'country',              'USA'),
    ('Chris Evert',             'grand_slam_titles',    '18'),
    ('Chris Evert',             'career_high_ranking',  '1'),
    ('Chris Evert',             'turned_pro_year',      '1972'),
    ('Chris Evert',             'career_titles',        '157'),

    ('Billie Jean King',        'active_status',        'Retired'),
    ('Billie Jean King',        'plays',                'Right'),
    ('Billie Jean King',        'backhand',             'One-Handed'),
    ('Billie Jean King',        'country',              'USA'),
    ('Billie Jean King',        'grand_slam_titles',    '12'),
    ('Billie Jean King',        'career_high_ranking',  '1'),
    ('Billie Jean King',        'turned_pro_year',      '1968'),
    ('Billie Jean King',        'career_titles',        '67'),

    ('Maria Sharapova',         'active_status',        'Retired'),
    ('Maria Sharapova',         'plays',                'Right'),
    ('Maria Sharapova',         'backhand',             'Two-Handed'),
    ('Maria Sharapova',         'country',              'Russia'),
    ('Maria Sharapova',         'grand_slam_titles',    '5'),
    ('Maria Sharapova',         'career_high_ranking',  '1'),
    ('Maria Sharapova',         'turned_pro_year',      '2001'),
    ('Maria Sharapova',         'career_titles',        '36'),

    ('Naomi Osaka',             'active_status',        'Active'),
    ('Naomi Osaka',             'plays',                'Right'),
    ('Naomi Osaka',             'backhand',             'Two-Handed'),
    ('Naomi Osaka',             'country',              'Japan'),
    ('Naomi Osaka',             'grand_slam_titles',    '4'),
    ('Naomi Osaka',             'career_high_ranking',  '1'),
    ('Naomi Osaka',             'turned_pro_year',      '2012'),
    ('Naomi Osaka',             'career_titles',        '7'),

    ('Monica Seles',            'active_status',        'Retired'),
    ('Monica Seles',            'plays',                'Left'),
    ('Monica Seles',            'backhand',             'Two-Handed'),
    ('Monica Seles',            'country',              'USA'),
    ('Monica Seles',            'grand_slam_titles',    '9'),
    ('Monica Seles',            'career_high_ranking',  '1'),
    ('Monica Seles',            'turned_pro_year',      '1989'),
    ('Monica Seles',            'career_titles',        '53'),

    ('Martina Hingis',          'active_status',        'Retired'),
    ('Martina Hingis',          'plays',                'Right'),
    ('Martina Hingis',          'backhand',             'Two-Handed'),
    ('Martina Hingis',          'country',              'Switzerland'),
    ('Martina Hingis',          'grand_slam_titles',    '5'),
    ('Martina Hingis',          'career_high_ranking',  '1'),
    ('Martina Hingis',          'turned_pro_year',      '1994'),
    ('Martina Hingis',          'career_titles',        '43'),

    ('Justine Henin',           'active_status',        'Retired'),
    ('Justine Henin',           'plays',                'Right'),
    ('Justine Henin',           'backhand',             'One-Handed'),
    ('Justine Henin',           'country',              'Belgium'),
    ('Justine Henin',           'grand_slam_titles',    '7'),
    ('Justine Henin',           'career_high_ranking',  '1'),
    ('Justine Henin',           'turned_pro_year',      '1999'),
    ('Justine Henin',           'career_titles',        '43'),

    ('Kim Clijsters',           'active_status',        'Retired'),
    ('Kim Clijsters',           'plays',                'Right'),
    ('Kim Clijsters',           'backhand',             'Two-Handed'),
    ('Kim Clijsters',           'country',              'Belgium'),
    ('Kim Clijsters',           'grand_slam_titles',    '4'),
    ('Kim Clijsters',           'career_high_ranking',  '1'),
    ('Kim Clijsters',           'turned_pro_year',      '1997'),
    ('Kim Clijsters',           'career_titles',        '41'),

    ('Lindsay Davenport',       'active_status',        'Retired'),
    ('Lindsay Davenport',       'plays',                'Right'),
    ('Lindsay Davenport',       'backhand',             'Two-Handed'),
    ('Lindsay Davenport',       'country',              'USA'),
    ('Lindsay Davenport',       'grand_slam_titles',    '3'),
    ('Lindsay Davenport',       'career_high_ranking',  '1'),
    ('Lindsay Davenport',       'turned_pro_year',      '1993'),
    ('Lindsay Davenport',       'career_titles',        '55'),

    ('Victoria Azarenka',       'active_status',        'Active'),
    ('Victoria Azarenka',       'plays',                'Right'),
    ('Victoria Azarenka',       'backhand',             'Two-Handed'),
    ('Victoria Azarenka',       'country',              'Belarus'),
    ('Victoria Azarenka',       'grand_slam_titles',    '2'),
    ('Victoria Azarenka',       'career_high_ranking',  '1'),
    ('Victoria Azarenka',       'turned_pro_year',      '2003'),
    ('Victoria Azarenka',       'career_titles',        '21'),

    ('Caroline Wozniacki',      'active_status',        'Retired'),
    ('Caroline Wozniacki',      'plays',                'Right'),
    ('Caroline Wozniacki',      'backhand',             'Two-Handed'),
    ('Caroline Wozniacki',      'country',              'Denmark'),
    ('Caroline Wozniacki',      'grand_slam_titles',    '1'),
    ('Caroline Wozniacki',      'career_high_ranking',  '1'),
    ('Caroline Wozniacki',      'turned_pro_year',      '2005'),
    ('Caroline Wozniacki',      'career_titles',        '30'),

    ('Coco Gauff',               'active_status',        'Active'),
    ('Coco Gauff',               'plays',                'Right'),
    ('Coco Gauff',               'backhand',             'Two-Handed'),
    ('Coco Gauff',               'country',              'USA'),
    ('Coco Gauff',               'grand_slam_titles',    '2'),
    ('Coco Gauff',               'career_high_ranking',  '2'),
    ('Coco Gauff',               'turned_pro_year',      '2018'),
    ('Coco Gauff',               'career_titles',        '11'),

    ('Iga Świątek',              'active_status',        'Active'),
    ('Iga Świątek',              'plays',                'Right'),
    ('Iga Świątek',              'backhand',             'Two-Handed'),
    ('Iga Świątek',              'country',              'Poland'),
    ('Iga Świątek',              'grand_slam_titles',    '6'),
    ('Iga Świątek',              'career_high_ranking',  '1'),
    ('Iga Świątek',              'turned_pro_year',      '2019'),
    ('Iga Świątek',              'career_titles',        '25'),

    ('Aryna Sabalenka',          'active_status',        'Active'),
    ('Aryna Sabalenka',          'plays',                'Right'),
    ('Aryna Sabalenka',          'backhand',             'Two-Handed'),
    ('Aryna Sabalenka',          'country',              'Belarus'),
    ('Aryna Sabalenka',          'grand_slam_titles',    '4'),
    ('Aryna Sabalenka',          'career_high_ranking',  '1'),
    ('Aryna Sabalenka',          'turned_pro_year',      '2015'),
    ('Aryna Sabalenka',          'career_titles',        '24'),

    ('Margaret Court',           'active_status',        'Retired'),
    ('Margaret Court',           'plays',                'Right'),
    ('Margaret Court',           'backhand',             'One-Handed'),
    ('Margaret Court',           'country',              'Australia'),
    ('Margaret Court',           'grand_slam_titles',    '24'),
    ('Margaret Court',           'career_high_ranking',  '1'),
    ('Margaret Court',           'turned_pro_year',      '1968'),
    ('Margaret Court',           'career_titles',        '92'),

    ('Li Na',                    'active_status',        'Retired'),
    ('Li Na',                    'plays',                'Right'),
    ('Li Na',                    'backhand',             'Two-Handed'),
    ('Li Na',                    'country',              'China'),
    ('Li Na',                    'grand_slam_titles',    '2'),
    ('Li Na',                    'career_high_ranking',  '2'),
    ('Li Na',                    'turned_pro_year',      '1999'),
    ('Li Na',                    'career_titles',        '9')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value"
WHERE "PlayerAttributeValues"."IsManuallyEdited" = false;