-- Tennis (men's) players, batch 5 (25 players, bringing the roster to 120
-- total: batch 1's 20 + batches 2-5's 25 each). Same upsert pattern as
-- prior batches: Players upserts on the unique Name constraint,
-- PlayerAttributeValues upserts on the existing unique (PlayerId,
-- AttributeDefinitionId) constraint. No overrides in this batch -- every
-- row is IsOverridden = false, DifficultyOverride = NULL.
--
-- Note: Cameron Norrie's country is normalized to "United Kingdom" (not
-- "UK"), matching the same normalization already applied to Jack Draper
-- in batch 2, to keep a single consistent value for this country.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Arthur Fils',              false, NULL),
    ('Jakub Menšík',             false, NULL),
    ('Learner Tien',             false, NULL),
    ('Cameron Norrie',           false, NULL),
    ('Roberto Bautista Agut',    false, NULL),
    ('Christopher Eubanks',      false, NULL),
    ('Alexei Popyrin',           false, NULL),
    ('Botic van de Zandschulp',  false, NULL),
    ('Tomás Martín Etcheverry',  false, NULL),
    ('Luciano Darderi',          false, NULL),
    ('Jiří Lehečka',             false, NULL),
    ('Brandon Nakashima',        false, NULL),
    ('Corentin Moutet',          false, NULL),
    ('Adrian Mannarino',         false, NULL),
    ('Valentin Vacherot',        false, NULL),
    ('João Fonseca',             false, NULL),
    ('Arthur Rinderknech',       false, NULL),

    ('Ivo Karlović',             false, NULL),
    ('Ivan Ljubičić',            false, NULL),
    ('Mikhail Youzhny',          false, NULL),
    ('Juan Mónaco',              false, NULL),
    ('Tommy Robredo',            false, NULL),
    ('Jürgen Melzer',            false, NULL),
    ('Alexandr Dolgopolov',      false, NULL),
    ('Marcel Granollers',        false, NULL)
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
    -- Player,                     AttrKey,                Value
    ('Arthur Fils',                'active_status',        'Active'),
    ('Arthur Fils',                'plays',                'Right'),
    ('Arthur Fils',                'backhand',             'Two-Handed'),
    ('Arthur Fils',                'country',              'France'),
    ('Arthur Fils',                'grand_slam_titles',    '0'),
    ('Arthur Fils',                'career_high_ranking',  '14'),
    ('Arthur Fils',                'turned_pro_year',      '2022'),
    ('Arthur Fils',                'career_titles',        '4'),

    ('Jakub Menšík',               'active_status',        'Active'),
    ('Jakub Menšík',               'plays',                'Right'),
    ('Jakub Menšík',               'backhand',             'Two-Handed'),
    ('Jakub Menšík',               'country',              'Czech Republic'),
    ('Jakub Menšík',               'grand_slam_titles',    '0'),
    ('Jakub Menšík',               'career_high_ranking',  '15'),
    ('Jakub Menšík',               'turned_pro_year',      '2022'),
    ('Jakub Menšík',               'career_titles',        '2'),

    ('Learner Tien',               'active_status',        'Active'),
    ('Learner Tien',               'plays',                'Left'),
    ('Learner Tien',               'backhand',             'Two-Handed'),
    ('Learner Tien',               'country',              'USA'),
    ('Learner Tien',               'grand_slam_titles',    '0'),
    ('Learner Tien',               'career_high_ranking',  '28'),
    ('Learner Tien',               'turned_pro_year',      '2023'),
    ('Learner Tien',               'career_titles',        '0'),

    ('Cameron Norrie',             'active_status',        'Active'),
    ('Cameron Norrie',             'plays',                'Left'),
    ('Cameron Norrie',             'backhand',             'Two-Handed'),
    ('Cameron Norrie',             'country',              'United Kingdom'),
    ('Cameron Norrie',             'grand_slam_titles',    '0'),
    ('Cameron Norrie',             'career_high_ranking',  '8'),
    ('Cameron Norrie',             'turned_pro_year',      '2017'),
    ('Cameron Norrie',             'career_titles',        '5'),

    ('Roberto Bautista Agut',      'active_status',        'Active'),
    ('Roberto Bautista Agut',      'plays',                'Right'),
    ('Roberto Bautista Agut',      'backhand',             'Two-Handed'),
    ('Roberto Bautista Agut',      'country',              'Spain'),
    ('Roberto Bautista Agut',      'grand_slam_titles',    '0'),
    ('Roberto Bautista Agut',      'career_high_ranking',  '9'),
    ('Roberto Bautista Agut',      'turned_pro_year',      '2010'),
    ('Roberto Bautista Agut',      'career_titles',        '5'),

    ('Christopher Eubanks',        'active_status',        'Active'),
    ('Christopher Eubanks',        'plays',                'Right'),
    ('Christopher Eubanks',        'backhand',             'Two-Handed'),
    ('Christopher Eubanks',        'country',              'USA'),
    ('Christopher Eubanks',        'grand_slam_titles',    '0'),
    ('Christopher Eubanks',        'career_high_ranking',  '29'),
    ('Christopher Eubanks',        'turned_pro_year',      '2017'),
    ('Christopher Eubanks',        'career_titles',        '1'),

    ('Alexei Popyrin',             'active_status',        'Active'),
    ('Alexei Popyrin',             'plays',                'Right'),
    ('Alexei Popyrin',             'backhand',             'Two-Handed'),
    ('Alexei Popyrin',             'country',              'Australia'),
    ('Alexei Popyrin',             'grand_slam_titles',    '0'),
    ('Alexei Popyrin',             'career_high_ranking',  '19'),
    ('Alexei Popyrin',             'turned_pro_year',      '2017'),
    ('Alexei Popyrin',             'career_titles',        '3'),

    ('Botic van de Zandschulp',    'active_status',        'Active'),
    ('Botic van de Zandschulp',    'plays',                'Right'),
    ('Botic van de Zandschulp',    'backhand',             'Two-Handed'),
    ('Botic van de Zandschulp',    'country',              'Netherlands'),
    ('Botic van de Zandschulp',    'grand_slam_titles',    '0'),
    ('Botic van de Zandschulp',    'career_high_ranking',  '22'),
    ('Botic van de Zandschulp',    'turned_pro_year',      '2019'),
    ('Botic van de Zandschulp',    'career_titles',        '1'),

    ('Tomás Martín Etcheverry',    'active_status',        'Active'),
    ('Tomás Martín Etcheverry',    'plays',                'Right'),
    ('Tomás Martín Etcheverry',    'backhand',             'Two-Handed'),
    ('Tomás Martín Etcheverry',    'country',              'Argentina'),
    ('Tomás Martín Etcheverry',    'grand_slam_titles',    '0'),
    ('Tomás Martín Etcheverry',    'career_high_ranking',  '25'),
    ('Tomás Martín Etcheverry',    'turned_pro_year',      '2019'),
    ('Tomás Martín Etcheverry',    'career_titles',        '1'),

    ('Luciano Darderi',            'active_status',        'Active'),
    ('Luciano Darderi',            'plays',                'Right'),
    ('Luciano Darderi',            'backhand',             'Two-Handed'),
    ('Luciano Darderi',            'country',              'Italy'),
    ('Luciano Darderi',            'grand_slam_titles',    '0'),
    ('Luciano Darderi',            'career_high_ranking',  '30'),
    ('Luciano Darderi',            'turned_pro_year',      '2021'),
    ('Luciano Darderi',            'career_titles',        '2'),

    ('Jiří Lehečka',               'active_status',        'Active'),
    ('Jiří Lehečka',               'plays',                'Right'),
    ('Jiří Lehečka',               'backhand',             'Two-Handed'),
    ('Jiří Lehečka',               'country',              'Czech Republic'),
    ('Jiří Lehečka',               'grand_slam_titles',    '0'),
    ('Jiří Lehečka',               'career_high_ranking',  '20'),
    ('Jiří Lehečka',               'turned_pro_year',      '2021'),
    ('Jiří Lehečka',               'career_titles',        '1'),

    ('Brandon Nakashima',          'active_status',        'Active'),
    ('Brandon Nakashima',          'plays',                'Right'),
    ('Brandon Nakashima',          'backhand',             'Two-Handed'),
    ('Brandon Nakashima',          'country',              'USA'),
    ('Brandon Nakashima',          'grand_slam_titles',    '0'),
    ('Brandon Nakashima',          'career_high_ranking',  '32'),
    ('Brandon Nakashima',          'turned_pro_year',      '2020'),
    ('Brandon Nakashima',          'career_titles',        '2'),

    ('Corentin Moutet',            'active_status',        'Active'),
    ('Corentin Moutet',            'plays',                'Left'),
    ('Corentin Moutet',            'backhand',             'Two-Handed'),
    ('Corentin Moutet',            'country',              'France'),
    ('Corentin Moutet',            'grand_slam_titles',    '0'),
    ('Corentin Moutet',            'career_high_ranking',  '39'),
    ('Corentin Moutet',            'turned_pro_year',      '2017'),
    ('Corentin Moutet',            'career_titles',        '1'),

    ('Adrian Mannarino',           'active_status',        'Active'),
    ('Adrian Mannarino',           'plays',                'Left'),
    ('Adrian Mannarino',           'backhand',             'Two-Handed'),
    ('Adrian Mannarino',           'country',              'France'),
    ('Adrian Mannarino',           'grand_slam_titles',    '0'),
    ('Adrian Mannarino',           'career_high_ranking',  '17'),
    ('Adrian Mannarino',           'turned_pro_year',      '2007'),
    ('Adrian Mannarino',           'career_titles',        '2'),

    ('Valentin Vacherot',          'active_status',        'Active'),
    ('Valentin Vacherot',          'plays',                'Right'),
    ('Valentin Vacherot',          'backhand',             'Two-Handed'),
    ('Valentin Vacherot',          'country',              'Monaco'),
    ('Valentin Vacherot',          'grand_slam_titles',    '0'),
    ('Valentin Vacherot',          'career_high_ranking',  '16'),
    ('Valentin Vacherot',          'turned_pro_year',      '2021'),
    ('Valentin Vacherot',          'career_titles',        '1'),

    ('João Fonseca',               'active_status',        'Active'),
    ('João Fonseca',               'plays',                'Right'),
    ('João Fonseca',               'backhand',             'Two-Handed'),
    ('João Fonseca',               'country',              'Brazil'),
    ('João Fonseca',               'grand_slam_titles',    '0'),
    ('João Fonseca',               'career_high_ranking',  '24'),
    ('João Fonseca',               'turned_pro_year',      '2024'),
    ('João Fonseca',               'career_titles',        '2'),

    ('Arthur Rinderknech',         'active_status',        'Active'),
    ('Arthur Rinderknech',         'plays',                'Right'),
    ('Arthur Rinderknech',         'backhand',             'Two-Handed'),
    ('Arthur Rinderknech',         'country',              'France'),
    ('Arthur Rinderknech',         'grand_slam_titles',    '0'),
    ('Arthur Rinderknech',         'career_high_ranking',  '24'),
    ('Arthur Rinderknech',         'turned_pro_year',      '2018'),
    ('Arthur Rinderknech',         'career_titles',        '0'),

    ('Ivo Karlović',               'active_status',        'Retired'),
    ('Ivo Karlović',               'plays',                'Right'),
    ('Ivo Karlović',               'backhand',             'One-Handed'),
    ('Ivo Karlović',               'country',              'Croatia'),
    ('Ivo Karlović',               'grand_slam_titles',    '0'),
    ('Ivo Karlović',               'career_high_ranking',  '14'),
    ('Ivo Karlović',               'turned_pro_year',      '2000'),
    ('Ivo Karlović',               'career_titles',        '8'),

    ('Ivan Ljubičić',              'active_status',        'Retired'),
    ('Ivan Ljubičić',              'plays',                'Right'),
    ('Ivan Ljubičić',              'backhand',             'One-Handed'),
    ('Ivan Ljubičić',              'country',              'Croatia'),
    ('Ivan Ljubičić',              'grand_slam_titles',    '0'),
    ('Ivan Ljubičić',              'career_high_ranking',  '3'),
    ('Ivan Ljubičić',              'turned_pro_year',      '1998'),
    ('Ivan Ljubičić',              'career_titles',        '10'),

    ('Mikhail Youzhny',            'active_status',        'Retired'),
    ('Mikhail Youzhny',            'plays',                'Right'),
    ('Mikhail Youzhny',            'backhand',             'One-Handed'),
    ('Mikhail Youzhny',            'country',              'Russia'),
    ('Mikhail Youzhny',            'grand_slam_titles',    '0'),
    ('Mikhail Youzhny',            'career_high_ranking',  '8'),
    ('Mikhail Youzhny',            'turned_pro_year',      '1999'),
    ('Mikhail Youzhny',            'career_titles',        '10'),

    ('Juan Mónaco',                'active_status',        'Retired'),
    ('Juan Mónaco',                'plays',                'Right'),
    ('Juan Mónaco',                'backhand',             'Two-Handed'),
    ('Juan Mónaco',                'country',              'Argentina'),
    ('Juan Mónaco',                'grand_slam_titles',    '0'),
    ('Juan Mónaco',                'career_high_ranking',  '10'),
    ('Juan Mónaco',                'turned_pro_year',      '2002'),
    ('Juan Mónaco',                'career_titles',        '9'),

    ('Tommy Robredo',              'active_status',        'Retired'),
    ('Tommy Robredo',              'plays',                'Right'),
    ('Tommy Robredo',              'backhand',             'One-Handed'),
    ('Tommy Robredo',              'country',              'Spain'),
    ('Tommy Robredo',              'grand_slam_titles',    '0'),
    ('Tommy Robredo',              'career_high_ranking',  '5'),
    ('Tommy Robredo',              'turned_pro_year',      '1998'),
    ('Tommy Robredo',              'career_titles',        '12'),

    ('Jürgen Melzer',              'active_status',        'Retired'),
    ('Jürgen Melzer',              'plays',                'Left'),
    ('Jürgen Melzer',              'backhand',             'Two-Handed'),
    ('Jürgen Melzer',              'country',              'Austria'),
    ('Jürgen Melzer',              'grand_slam_titles',    '0'),
    ('Jürgen Melzer',              'career_high_ranking',  '8'),
    ('Jürgen Melzer',              'turned_pro_year',      '1999'),
    ('Jürgen Melzer',              'career_titles',        '5'),

    ('Alexandr Dolgopolov',        'active_status',        'Retired'),
    ('Alexandr Dolgopolov',        'plays',                'Right'),
    ('Alexandr Dolgopolov',        'backhand',             'Two-Handed'),
    ('Alexandr Dolgopolov',        'country',              'Ukraine'),
    ('Alexandr Dolgopolov',        'grand_slam_titles',    '0'),
    ('Alexandr Dolgopolov',        'career_high_ranking',  '13'),
    ('Alexandr Dolgopolov',        'turned_pro_year',      '2006'),
    ('Alexandr Dolgopolov',        'career_titles',        '3'),

    ('Marcel Granollers',          'active_status',        'Active'),
    ('Marcel Granollers',          'plays',                'Right'),
    ('Marcel Granollers',          'backhand',             'Two-Handed'),
    ('Marcel Granollers',          'country',              'Spain'),
    ('Marcel Granollers',          'grand_slam_titles',    '0'),
    ('Marcel Granollers',          'career_high_ranking',  '19'),
    ('Marcel Granollers',          'turned_pro_year',      '2003'),
    ('Marcel Granollers',          'career_titles',        '4')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";