-- Tennis (men's) players, batch 7 (25 players, bringing the roster to 170
-- total: batch 1's 20 + batches 2-7's 25 each). Same upsert pattern as
-- prior batches: Players upserts on the unique Name constraint,
-- PlayerAttributeValues upserts on the existing unique (PlayerId,
-- AttributeDefinitionId) constraint. No overrides in this batch -- every
-- row is IsOverridden = false, DifficultyOverride = NULL.
--
-- Note: Greg Rusedski's country is normalized to "United Kingdom" (not
-- "UK"), matching the same normalization already applied to Jack Draper
-- (batch 2), Cameron Norrie (batch 5), and Dan Evans/Fred Perry/Kyle
-- Edmund (batch 6), to keep a single consistent value for this country.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Richard Krajicek',           false, NULL),
    ('Thomas Enqvist',             false, NULL),
    ('Wayne Ferreira',             false, NULL),
    ('Greg Rusedski',              false, NULL),
    ('Albert Costa',               false, NULL),
    ('Pablo Cuevas',               false, NULL),
    ('Magnus Norman',              false, NULL),
    ('Gastón Gaudio',              false, NULL),
    ('Félix Mantilla',             false, NULL),
    ('Todd Martin',                false, NULL),
    ('Younes El Aynaoui',          false, NULL),
    ('Jonas Björkman',             false, NULL),
    ('Nicolás Massú',              false, NULL),
    ('Cédric Pioline',             false, NULL),
    ('Lorenzo Sonego',             false, NULL),
    ('Bernard Tomic',              false, NULL),
    ('Alberto Berasategui',        false, NULL),
    ('Pablo Andújar',              false, NULL),
    ('Pat Cash',                   false, NULL),
    ('René Lacoste',               false, NULL),
    ('Cristian Garín',             false, NULL),
    ('Benoît Paire',               false, NULL),
    ('Albert Ramos Viñolas',       false, NULL),
    ('John Millman',               false, NULL),
    ('Jan-Lennard Struff',         false, NULL)
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
    ('Richard Krajicek',              'active_status',        'Retired'),
    ('Richard Krajicek',              'plays',                'Right'),
    ('Richard Krajicek',              'backhand',             'One-Handed'),
    ('Richard Krajicek',              'country',              'Netherlands'),
    ('Richard Krajicek',              'grand_slam_titles',    '1'),
    ('Richard Krajicek',              'career_high_ranking',  '4'),
    ('Richard Krajicek',              'turned_pro_year',      '1989'),
    ('Richard Krajicek',              'career_titles',        '17'),

    ('Thomas Enqvist',                'active_status',        'Retired'),
    ('Thomas Enqvist',                'plays',                'Right'),
    ('Thomas Enqvist',                'backhand',             'Two-Handed'),
    ('Thomas Enqvist',                'country',              'Sweden'),
    ('Thomas Enqvist',                'grand_slam_titles',    '0'),
    ('Thomas Enqvist',                'career_high_ranking',  '4'),
    ('Thomas Enqvist',                'turned_pro_year',      '1991'),
    ('Thomas Enqvist',                'career_titles',        '19'),

    ('Wayne Ferreira',                'active_status',        'Retired'),
    ('Wayne Ferreira',                'plays',                'Right'),
    ('Wayne Ferreira',                'backhand',             'Two-Handed'),
    ('Wayne Ferreira',                'country',              'South Africa'),
    ('Wayne Ferreira',                'grand_slam_titles',    '0'),
    ('Wayne Ferreira',                'career_high_ranking',  '6'),
    ('Wayne Ferreira',                'turned_pro_year',      '1989'),
    ('Wayne Ferreira',                'career_titles',        '15'),

    ('Greg Rusedski',                 'active_status',        'Retired'),
    ('Greg Rusedski',                 'plays',                'Left'),
    ('Greg Rusedski',                 'backhand',             'One-Handed'),
    ('Greg Rusedski',                 'country',              'United Kingdom'),
    ('Greg Rusedski',                 'grand_slam_titles',    '0'),
    ('Greg Rusedski',                 'career_high_ranking',  '4'),
    ('Greg Rusedski',                 'turned_pro_year',      '1991'),
    ('Greg Rusedski',                 'career_titles',        '15'),

    ('Albert Costa',                  'active_status',        'Retired'),
    ('Albert Costa',                  'plays',                'Right'),
    ('Albert Costa',                  'backhand',             'One-Handed'),
    ('Albert Costa',                  'country',              'Spain'),
    ('Albert Costa',                  'grand_slam_titles',    '1'),
    ('Albert Costa',                  'career_high_ranking',  '6'),
    ('Albert Costa',                  'turned_pro_year',      '1993'),
    ('Albert Costa',                  'career_titles',        '12'),

    ('Pablo Cuevas',                  'active_status',        'Retired'),
    ('Pablo Cuevas',                  'plays',                'Right'),
    ('Pablo Cuevas',                  'backhand',             'One-Handed'),
    ('Pablo Cuevas',                  'country',              'Uruguay'),
    ('Pablo Cuevas',                  'grand_slam_titles',    '0'),
    ('Pablo Cuevas',                  'career_high_ranking',  '19'),
    ('Pablo Cuevas',                  'turned_pro_year',      '2004'),
    ('Pablo Cuevas',                  'career_titles',        '6'),

    ('Magnus Norman',                 'active_status',        'Retired'),
    ('Magnus Norman',                 'plays',                'Right'),
    ('Magnus Norman',                 'backhand',             'Two-Handed'),
    ('Magnus Norman',                 'country',              'Sweden'),
    ('Magnus Norman',                 'grand_slam_titles',    '0'),
    ('Magnus Norman',                 'career_high_ranking',  '2'),
    ('Magnus Norman',                 'turned_pro_year',      '1995'),
    ('Magnus Norman',                 'career_titles',        '12'),

    ('Gastón Gaudio',                 'active_status',        'Retired'),
    ('Gastón Gaudio',                 'plays',                'Right'),
    ('Gastón Gaudio',                 'backhand',             'One-Handed'),
    ('Gastón Gaudio',                 'country',              'Argentina'),
    ('Gastón Gaudio',                 'grand_slam_titles',    '1'),
    ('Gastón Gaudio',                 'career_high_ranking',  '5'),
    ('Gastón Gaudio',                 'turned_pro_year',      '1996'),
    ('Gastón Gaudio',                 'career_titles',        '8'),

    ('Félix Mantilla',                'active_status',        'Retired'),
    ('Félix Mantilla',                'plays',                'Right'),
    ('Félix Mantilla',                'backhand',             'One-Handed'),
    ('Félix Mantilla',                'country',              'Spain'),
    ('Félix Mantilla',                'grand_slam_titles',    '0'),
    ('Félix Mantilla',                'career_high_ranking',  '10'),
    ('Félix Mantilla',                'turned_pro_year',      '1993'),
    ('Félix Mantilla',                'career_titles',        '10'),

    ('Todd Martin',                   'active_status',        'Retired'),
    ('Todd Martin',                   'plays',                'Right'),
    ('Todd Martin',                   'backhand',             'Two-Handed'),
    ('Todd Martin',                   'country',              'USA'),
    ('Todd Martin',                   'grand_slam_titles',    '0'),
    ('Todd Martin',                   'career_high_ranking',  '4'),
    ('Todd Martin',                   'turned_pro_year',      '1990'),
    ('Todd Martin',                   'career_titles',        '8'),

    ('Younes El Aynaoui',             'active_status',        'Retired'),
    ('Younes El Aynaoui',             'plays',                'Right'),
    ('Younes El Aynaoui',             'backhand',             'Two-Handed'),
    ('Younes El Aynaoui',             'country',              'Morocco'),
    ('Younes El Aynaoui',             'grand_slam_titles',    '0'),
    ('Younes El Aynaoui',             'career_high_ranking',  '14'),
    ('Younes El Aynaoui',             'turned_pro_year',      '1990'),
    ('Younes El Aynaoui',             'career_titles',        '5'),

    ('Jonas Björkman',                'active_status',        'Retired'),
    ('Jonas Björkman',                'plays',                'Right'),
    ('Jonas Björkman',                'backhand',             'Two-Handed'),
    ('Jonas Björkman',                'country',              'Sweden'),
    ('Jonas Björkman',                'grand_slam_titles',    '0'),
    ('Jonas Björkman',                'career_high_ranking',  '4'),
    ('Jonas Björkman',                'turned_pro_year',      '1991'),
    ('Jonas Björkman',                'career_titles',        '6'),

    ('Nicolás Massú',                 'active_status',        'Retired'),
    ('Nicolás Massú',                 'plays',                'Right'),
    ('Nicolás Massú',                 'backhand',             'Two-Handed'),
    ('Nicolás Massú',                 'country',              'Chile'),
    ('Nicolás Massú',                 'grand_slam_titles',    '0'),
    ('Nicolás Massú',                 'career_high_ranking',  '9'),
    ('Nicolás Massú',                 'turned_pro_year',      '1997'),
    ('Nicolás Massú',                 'career_titles',        '6'),

    ('Cédric Pioline',                'active_status',        'Retired'),
    ('Cédric Pioline',                'plays',                'Right'),
    ('Cédric Pioline',                'backhand',             'One-Handed'),
    ('Cédric Pioline',                'country',              'France'),
    ('Cédric Pioline',                'grand_slam_titles',    '0'),
    ('Cédric Pioline',                'career_high_ranking',  '5'),
    ('Cédric Pioline',                'turned_pro_year',      '1989'),
    ('Cédric Pioline',                'career_titles',        '5'),

    ('Lorenzo Sonego',                'active_status',        'Active'),
    ('Lorenzo Sonego',                'plays',                'Right'),
    ('Lorenzo Sonego',                'backhand',             'Two-Handed'),
    ('Lorenzo Sonego',                'country',              'Italy'),
    ('Lorenzo Sonego',                'grand_slam_titles',    '0'),
    ('Lorenzo Sonego',                'career_high_ranking',  '21'),
    ('Lorenzo Sonego',                'turned_pro_year',      '2013'),
    ('Lorenzo Sonego',                'career_titles',        '4'),

    ('Bernard Tomic',                 'active_status',        'Active'),
    ('Bernard Tomic',                 'plays',                'Right'),
    ('Bernard Tomic',                 'backhand',             'Two-Handed'),
    ('Bernard Tomic',                 'country',              'Australia'),
    ('Bernard Tomic',                 'grand_slam_titles',    '0'),
    ('Bernard Tomic',                 'career_high_ranking',  '17'),
    ('Bernard Tomic',                 'turned_pro_year',      '2008'),
    ('Bernard Tomic',                 'career_titles',        '4'),

    ('Alberto Berasategui',           'active_status',        'Retired'),
    ('Alberto Berasategui',           'plays',                'Right'),
    ('Alberto Berasategui',           'backhand',             'One-Handed'),
    ('Alberto Berasategui',           'country',              'Spain'),
    ('Alberto Berasategui',           'grand_slam_titles',    '0'),
    ('Alberto Berasategui',           'career_high_ranking',  '7'),
    ('Alberto Berasategui',           'turned_pro_year',      '1991'),
    ('Alberto Berasategui',           'career_titles',        '14'),

    ('Pablo Andújar',                 'active_status',        'Retired'),
    ('Pablo Andújar',                 'plays',                'Right'),
    ('Pablo Andújar',                 'backhand',             'Two-Handed'),
    ('Pablo Andújar',                 'country',              'Spain'),
    ('Pablo Andújar',                 'grand_slam_titles',    '0'),
    ('Pablo Andújar',                 'career_high_ranking',  '32'),
    ('Pablo Andújar',                 'turned_pro_year',      '2004'),
    ('Pablo Andújar',                 'career_titles',        '4'),

    ('Pat Cash',                      'active_status',        'Retired'),
    ('Pat Cash',                      'plays',                'Right'),
    ('Pat Cash',                      'backhand',             'One-Handed'),
    ('Pat Cash',                      'country',              'Australia'),
    ('Pat Cash',                      'grand_slam_titles',    '1'),
    ('Pat Cash',                      'career_high_ranking',  '4'),
    ('Pat Cash',                      'turned_pro_year',      '1982'),
    ('Pat Cash',                      'career_titles',        '6'),

    ('René Lacoste',                  'active_status',        'Retired'),
    ('René Lacoste',                  'plays',                'Right'),
    ('René Lacoste',                  'backhand',             'One-Handed'),
    ('René Lacoste',                  'country',              'France'),
    ('René Lacoste',                  'grand_slam_titles',    '7'),
    ('René Lacoste',                  'career_high_ranking',  '1'),
    ('René Lacoste',                  'turned_pro_year',      '1922'),
    ('René Lacoste',                  'career_titles',        '24'),

    ('Cristian Garín',                'active_status',        'Active'),
    ('Cristian Garín',                'plays',                'Right'),
    ('Cristian Garín',                'backhand',             'Two-Handed'),
    ('Cristian Garín',                'country',              'Chile'),
    ('Cristian Garín',                'grand_slam_titles',    '0'),
    ('Cristian Garín',                'career_high_ranking',  '17'),
    ('Cristian Garín',                'turned_pro_year',      '2011'),
    ('Cristian Garín',                'career_titles',        '5'),

    ('Benoît Paire',                  'active_status',        'Retired'),
    ('Benoît Paire',                  'plays',                'Right'),
    ('Benoît Paire',                  'backhand',             'Two-Handed'),
    ('Benoît Paire',                  'country',              'France'),
    ('Benoît Paire',                  'grand_slam_titles',    '0'),
    ('Benoît Paire',                  'career_high_ranking',  '18'),
    ('Benoît Paire',                  'turned_pro_year',      '2007'),
    ('Benoît Paire',                  'career_titles',        '3'),

    ('Albert Ramos Viñolas',          'active_status',        'Retired'),
    ('Albert Ramos Viñolas',          'plays',                'Left'),
    ('Albert Ramos Viñolas',          'backhand',             'Two-Handed'),
    ('Albert Ramos Viñolas',          'country',              'Spain'),
    ('Albert Ramos Viñolas',          'grand_slam_titles',    '0'),
    ('Albert Ramos Viñolas',          'career_high_ranking',  '17'),
    ('Albert Ramos Viñolas',          'turned_pro_year',      '2007'),
    ('Albert Ramos Viñolas',          'career_titles',        '4'),

    ('John Millman',                  'active_status',        'Retired'),
    ('John Millman',                  'plays',                'Right'),
    ('John Millman',                  'backhand',             'Two-Handed'),
    ('John Millman',                  'country',              'Australia'),
    ('John Millman',                  'grand_slam_titles',    '0'),
    ('John Millman',                  'career_high_ranking',  '33'),
    ('John Millman',                  'turned_pro_year',      '2006'),
    ('John Millman',                  'career_titles',        '1'),

    ('Jan-Lennard Struff',            'active_status',        'Active'),
    ('Jan-Lennard Struff',            'plays',                'Right'),
    ('Jan-Lennard Struff',            'backhand',             'Two-Handed'),
    ('Jan-Lennard Struff',            'country',              'Germany'),
    ('Jan-Lennard Struff',            'grand_slam_titles',    '0'),
    ('Jan-Lennard Struff',            'career_high_ranking',  '21'),
    ('Jan-Lennard Struff',            'turned_pro_year',      '2009'),
    ('Jan-Lennard Struff',            'career_titles',        '1')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";