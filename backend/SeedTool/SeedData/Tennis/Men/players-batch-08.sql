-- Tennis (men's) players, batch 8 (22 players, bringing the roster to 192
-- total: batch 1's 20 + batches 2-7's 25 each + batch 8's 22). Same upsert
-- pattern as prior batches: Players upserts on the unique Name constraint,
-- PlayerAttributeValues upserts on the existing unique (PlayerId,
-- AttributeDefinitionId) constraint. No overrides in this batch -- every
-- row is IsOverridden = false, DifficultyOverride = NULL.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Manuel Santana',             false, NULL),
    ('Marcelo Ríos',               false, NULL),
    ('Aleksandar Vukic',           false, NULL),
    ('Jenson Brooksby',            false, NULL),
    ('Camilo Ugo Carabelli',       false, NULL),
    ('Emil Ruusuvuori',            false, NULL),
    ('Daniel Altmaier',            false, NULL),
    ('Tallon Griekspoor',          false, NULL),
    ('Térence Atmane',             false, NULL),
    ('Zizou Bergs',                false, NULL),
    ('Yannick Hanfmann',           false, NULL),
    ('Marcos Giron',               false, NULL),
    ('J.J. Wolf',                  false, NULL),
    ('Matteo Arnaldi',             false, NULL),
    ('Borna Ćorić',                false, NULL),
    ('Ignacio Buse',               false, NULL),
    ('Juan Manuel Cerúndolo',      false, NULL),
    ('Nuno Borges',                false, NULL),
    ('Raphaël Collignon',          false, NULL),
    ('Denis Istomin',              false, NULL),
    ('João Sousa',                 false, NULL),
    ('Steve Johnson',              false, NULL)
) AS v("Name", "IsOverridden", "DifficultyOverride")
WHERE s."Slug" = 'tennis'
ON CONFLICT ("Name") DO UPDATE SET
    "SportId" = EXCLUDED."SportId",
    "IsOverridden" = EXCLUDED."IsOverridden",
    "DifficultyOverride" = EXCLUDED."DifficultyOverride";

-- One row per (player, attribute) pair. PlayerId/AttributeDefinitionId are
-- resolved by name/key lookup rather than hardcoded ids, so this file has
-- no dependency on insertion order or id values.
INSERT INTO "PlayerAttributeValues" ("PlayerId", "AttributeDefinitionId", "Value")
SELECT p."Id", ad."Id", v."Value"
FROM (VALUES
    -- Player,                        AttrKey,                Value
    ('Manuel Santana',                'active_status',        'Retired'),
    ('Manuel Santana',                'plays',                'Right'),
    ('Manuel Santana',                'backhand',             'One-Handed'),
    ('Manuel Santana',                'country',              'Spain'),
    ('Manuel Santana',                'grand_slam_titles',    '3'),
    ('Manuel Santana',                'career_high_ranking',  '1'),
    ('Manuel Santana',                'turned_pro_year',      '1956'),
    ('Manuel Santana',                'career_titles',        '94'),

    ('Marcelo Ríos',                  'active_status',        'Retired'),
    ('Marcelo Ríos',                  'plays',                'Left'),
    ('Marcelo Ríos',                  'backhand',             'Two-Handed'),
    ('Marcelo Ríos',                  'country',              'Chile'),
    ('Marcelo Ríos',                  'grand_slam_titles',    '0'),
    ('Marcelo Ríos',                  'career_high_ranking',  '1'),
    ('Marcelo Ríos',                  'turned_pro_year',      '1994'),
    ('Marcelo Ríos',                  'career_titles',        '18'),

    ('Aleksandar Vukic',              'active_status',        'Active'),
    ('Aleksandar Vukic',              'plays',                'Right'),
    ('Aleksandar Vukic',              'backhand',             'Two-Handed'),
    ('Aleksandar Vukic',              'country',              'Australia'),
    ('Aleksandar Vukic',              'grand_slam_titles',    '0'),
    ('Aleksandar Vukic',              'career_high_ranking',  '48'),
    ('Aleksandar Vukic',              'turned_pro_year',      '2018'),
    ('Aleksandar Vukic',              'career_titles',        '0'),

    ('Jenson Brooksby',                'active_status',        'Active'),
    ('Jenson Brooksby',                'plays',                'Right'),
    ('Jenson Brooksby',                'backhand',             'Two-Handed'),
    ('Jenson Brooksby',                'country',              'USA'),
    ('Jenson Brooksby',                'grand_slam_titles',    '0'),
    ('Jenson Brooksby',                'career_high_ranking',  '33'),
    ('Jenson Brooksby',                'turned_pro_year',      '2021'),
    ('Jenson Brooksby',                'career_titles',        '1'),

    ('Camilo Ugo Carabelli',           'active_status',        'Active'),
    ('Camilo Ugo Carabelli',           'plays',                'Right'),
    ('Camilo Ugo Carabelli',           'backhand',             'Two-Handed'),
    ('Camilo Ugo Carabelli',           'country',              'Argentina'),
    ('Camilo Ugo Carabelli',           'grand_slam_titles',    '0'),
    ('Camilo Ugo Carabelli',           'career_high_ranking',  '43'),
    ('Camilo Ugo Carabelli',           'turned_pro_year',      '2016'),
    ('Camilo Ugo Carabelli',           'career_titles',        '0'),

    ('Emil Ruusuvuori',                'active_status',        'Active'),
    ('Emil Ruusuvuori',                'plays',                'Right'),
    ('Emil Ruusuvuori',                'backhand',             'Two-Handed'),
    ('Emil Ruusuvuori',                'country',              'Finland'),
    ('Emil Ruusuvuori',                'grand_slam_titles',    '0'),
    ('Emil Ruusuvuori',                'career_high_ranking',  '37'),
    ('Emil Ruusuvuori',                'turned_pro_year',      '2018'),
    ('Emil Ruusuvuori',                'career_titles',        '0'),

    ('Daniel Altmaier',                'active_status',        'Active'),
    ('Daniel Altmaier',                'plays',                'Right'),
    ('Daniel Altmaier',                'backhand',             'One-Handed'),
    ('Daniel Altmaier',                'country',              'Germany'),
    ('Daniel Altmaier',                'grand_slam_titles',    '0'),
    ('Daniel Altmaier',                'career_high_ranking',  '44'),
    ('Daniel Altmaier',                'turned_pro_year',      '2014'),
    ('Daniel Altmaier',                'career_titles',        '0'),

    ('Tallon Griekspoor',              'active_status',        'Active'),
    ('Tallon Griekspoor',              'plays',                'Right'),
    ('Tallon Griekspoor',              'backhand',             'Two-Handed'),
    ('Tallon Griekspoor',              'country',              'Netherlands'),
    ('Tallon Griekspoor',              'grand_slam_titles',    '0'),
    ('Tallon Griekspoor',              'career_high_ranking',  '21'),
    ('Tallon Griekspoor',              'turned_pro_year',      '2015'),
    ('Tallon Griekspoor',              'career_titles',        '3'),

    ('Térence Atmane',                 'active_status',        'Active'),
    ('Térence Atmane',                 'plays',                'Left'),
    ('Térence Atmane',                 'backhand',             'Two-Handed'),
    ('Térence Atmane',                 'country',              'France'),
    ('Térence Atmane',                 'grand_slam_titles',    '0'),
    ('Térence Atmane',                 'career_high_ranking',  '41'),
    ('Térence Atmane',                 'turned_pro_year',      '2020'),
    ('Térence Atmane',                 'career_titles',        '0'),

    ('Zizou Bergs',                    'active_status',        'Active'),
    ('Zizou Bergs',                    'plays',                'Right'),
    ('Zizou Bergs',                    'backhand',             'Two-Handed'),
    ('Zizou Bergs',                    'country',              'Belgium'),
    ('Zizou Bergs',                    'grand_slam_titles',    '0'),
    ('Zizou Bergs',                    'career_high_ranking',  '38'),
    ('Zizou Bergs',                    'turned_pro_year',      '2018'),
    ('Zizou Bergs',                    'career_titles',        '0'),

    ('Yannick Hanfmann',               'active_status',        'Active'),
    ('Yannick Hanfmann',               'plays',                'Right'),
    ('Yannick Hanfmann',               'backhand',             'Two-Handed'),
    ('Yannick Hanfmann',               'country',              'Germany'),
    ('Yannick Hanfmann',               'grand_slam_titles',    '0'),
    ('Yannick Hanfmann',               'career_high_ranking',  '45'),
    ('Yannick Hanfmann',               'turned_pro_year',      '2015'),
    ('Yannick Hanfmann',               'career_titles',        '0'),

    ('Marcos Giron',                   'active_status',        'Active'),
    ('Marcos Giron',                   'plays',                'Right'),
    ('Marcos Giron',                   'backhand',             'Two-Handed'),
    ('Marcos Giron',                   'country',              'USA'),
    ('Marcos Giron',                   'grand_slam_titles',    '0'),
    ('Marcos Giron',                   'career_high_ranking',  '37'),
    ('Marcos Giron',                   'turned_pro_year',      '2014'),
    ('Marcos Giron',                   'career_titles',        '1'),

    ('J.J. Wolf',                      'active_status',        'Active'),
    ('J.J. Wolf',                      'plays',                'Right'),
    ('J.J. Wolf',                      'backhand',             'Two-Handed'),
    ('J.J. Wolf',                      'country',              'USA'),
    ('J.J. Wolf',                      'grand_slam_titles',    '0'),
    ('J.J. Wolf',                      'career_high_ranking',  '39'),
    ('J.J. Wolf',                      'turned_pro_year',      '2019'),
    ('J.J. Wolf',                      'career_titles',        '0'),

    ('Matteo Arnaldi',                 'active_status',        'Active'),
    ('Matteo Arnaldi',                 'plays',                'Right'),
    ('Matteo Arnaldi',                 'backhand',             'Two-Handed'),
    ('Matteo Arnaldi',                 'country',              'Italy'),
    ('Matteo Arnaldi',                 'grand_slam_titles',    '0'),
    ('Matteo Arnaldi',                 'career_high_ranking',  '30'),
    ('Matteo Arnaldi',                 'turned_pro_year',      '2019'),
    ('Matteo Arnaldi',                 'career_titles',        '0'),

    ('Borna Ćorić',                    'active_status',        'Active'),
    ('Borna Ćorić',                    'plays',                'Right'),
    ('Borna Ćorić',                    'backhand',             'Two-Handed'),
    ('Borna Ćorić',                    'country',              'Croatia'),
    ('Borna Ćorić',                    'grand_slam_titles',    '0'),
    ('Borna Ćorić',                    'career_high_ranking',  '12'),
    ('Borna Ćorić',                    'turned_pro_year',      '2013'),
    ('Borna Ćorić',                    'career_titles',        '3'),

    ('Ignacio Buse',                   'active_status',        'Active'),
    ('Ignacio Buse',                   'plays',                'Right'),
    ('Ignacio Buse',                   'backhand',             'Two-Handed'),
    ('Ignacio Buse',                   'country',              'Peru'),
    ('Ignacio Buse',                   'grand_slam_titles',    '0'),
    ('Ignacio Buse',                   'career_high_ranking',  '31'),
    ('Ignacio Buse',                   'turned_pro_year',      '2021'),
    ('Ignacio Buse',                   'career_titles',        '1'),

    ('Juan Manuel Cerúndolo',          'active_status',        'Active'),
    ('Juan Manuel Cerúndolo',          'plays',                'Left'),
    ('Juan Manuel Cerúndolo',          'backhand',             'Two-Handed'),
    ('Juan Manuel Cerúndolo',          'country',              'Argentina'),
    ('Juan Manuel Cerúndolo',          'grand_slam_titles',    '0'),
    ('Juan Manuel Cerúndolo',          'career_high_ranking',  '42'),
    ('Juan Manuel Cerúndolo',          'turned_pro_year',      '2018'),
    ('Juan Manuel Cerúndolo',          'career_titles',        '1'),

    ('Nuno Borges',                    'active_status',        'Active'),
    ('Nuno Borges',                    'plays',                'Right'),
    ('Nuno Borges',                    'backhand',             'Two-Handed'),
    ('Nuno Borges',                    'country',              'Portugal'),
    ('Nuno Borges',                    'grand_slam_titles',    '0'),
    ('Nuno Borges',                    'career_high_ranking',  '30'),
    ('Nuno Borges',                    'turned_pro_year',      '2019'),
    ('Nuno Borges',                    'career_titles',        '1'),

    ('Raphaël Collignon',              'active_status',        'Active'),
    ('Raphaël Collignon',              'plays',                'Right'),
    ('Raphaël Collignon',              'backhand',             'Two-Handed'),
    ('Raphaël Collignon',              'country',              'Belgium'),
    ('Raphaël Collignon',              'grand_slam_titles',    '0'),
    ('Raphaël Collignon',              'career_high_ranking',  '51'),
    ('Raphaël Collignon',              'turned_pro_year',      '2022'),
    ('Raphaël Collignon',              'career_titles',        '0'),

    ('Denis Istomin',                  'active_status',        'Retired'),
    ('Denis Istomin',                  'plays',                'Right'),
    ('Denis Istomin',                  'backhand',             'Two-Handed'),
    ('Denis Istomin',                  'country',              'Uzbekistan'),
    ('Denis Istomin',                  'grand_slam_titles',    '0'),
    ('Denis Istomin',                  'career_high_ranking',  '33'),
    ('Denis Istomin',                  'turned_pro_year',      '2004'),
    ('Denis Istomin',                  'career_titles',        '2'),

    ('João Sousa',                     'active_status',        'Retired'),
    ('João Sousa',                     'plays',                'Right'),
    ('João Sousa',                     'backhand',             'Two-Handed'),
    ('João Sousa',                     'country',              'Portugal'),
    ('João Sousa',                     'grand_slam_titles',    '0'),
    ('João Sousa',                     'career_high_ranking',  '28'),
    ('João Sousa',                     'turned_pro_year',      '2008'),
    ('João Sousa',                     'career_titles',        '4'),

    ('Steve Johnson',                  'active_status',        'Retired'),
    ('Steve Johnson',                  'plays',                'Right'),
    ('Steve Johnson',                  'backhand',             'Two-Handed'),
    ('Steve Johnson',                  'country',              'USA'),
    ('Steve Johnson',                  'grand_slam_titles',    '0'),
    ('Steve Johnson',                  'career_high_ranking',  '21'),
    ('Steve Johnson',                  'turned_pro_year',      '2012'),
    ('Steve Johnson',                  'career_titles',        '4')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";