-- Tennis (men's) players, batch 2 (25 players, bringing the roster to 45
-- total alongside batch 1's original 20). Same upsert pattern as batch 1:
-- Players upserts on the unique Name constraint, PlayerAttributeValues
-- upserts on the existing unique (PlayerId, AttributeDefinitionId)
-- constraint. Both constraints added/confirmed in migration
-- AddPlayerNameAndAttributeDefinitionUniqueConstraints.
--
-- Note: Jack Draper's country is normalized to "United Kingdom" (not
-- "UK") to match the value already used for Andy Murray in batch 1.
--
-- Grigor Dimitrov is overridden to Medium difficulty. His raw stats
-- (9 titles, never reached #1, no Grand Slam) would compute to Hard, but
-- that undersells his real-world recognizability -- former World No. 3,
-- well-known nickname ("Baby Fed"), ATP Finals champion, long
-- high-profile career -- so Medium is the more accurate difficulty for
-- casual player recognition.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Frances Tiafoe',        false, NULL),
    ('Grigor Dimitrov',       true,  'Medium'),
    ('Ben Shelton',           false, NULL),
    ('Tommy Paul',            false, NULL),
    ('Ugo Humbert',           false, NULL),
    ('Alex de Minaur',        false, NULL),
    ('Karen Khachanov',       false, NULL),
    ('Hubert Hurkacz',        false, NULL),
    ('Felix Auger-Aliassime', false, NULL),
    ('Sebastian Korda',       false, NULL),
    ('Francisco Cerundolo',   false, NULL),
    ('Alexander Bublik',      false, NULL),
    ('Flavio Cobolli',        false, NULL),
    ('Jack Draper',           false, NULL),

    ('Juan Martín del Potro', false, NULL),
    ('Marin Čilić',           false, NULL),
    ('Kei Nishikori',         false, NULL),
    ('Gaël Monfils',          false, NULL),
    ('Tomáš Berdych',         false, NULL),
    ('Richard Gasquet',       false, NULL),
    ('Nicolás Almagro',       false, NULL),

    ('Ivan Lendl',            false, NULL),
    ('Björn Borg',            false, NULL),
    ('Jimmy Connors',         false, NULL),
    ('Guillermo Vilas',       false, NULL)
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
    -- Player,                   AttrKey,                Value
    ('Frances Tiafoe',           'active_status',        'Active'),
    ('Frances Tiafoe',           'plays',                'Right'),
    ('Frances Tiafoe',           'backhand',             'Two-Handed'),
    ('Frances Tiafoe',           'country',              'USA'),
    ('Frances Tiafoe',           'grand_slam_titles',    '0'),
    ('Frances Tiafoe',           'career_high_ranking',  '10'),
    ('Frances Tiafoe',           'turned_pro_year',      '2015'),
    ('Frances Tiafoe',           'career_titles',        '4'),

    ('Grigor Dimitrov',          'active_status',        'Active'),
    ('Grigor Dimitrov',          'plays',                'Right'),
    ('Grigor Dimitrov',          'backhand',              'One-Handed'),
    ('Grigor Dimitrov',          'country',              'Bulgaria'),
    ('Grigor Dimitrov',          'grand_slam_titles',    '0'),
    ('Grigor Dimitrov',          'career_high_ranking',  '3'),
    ('Grigor Dimitrov',          'turned_pro_year',      '2008'),
    ('Grigor Dimitrov',          'career_titles',        '9'),

    ('Ben Shelton',              'active_status',        'Active'),
    ('Ben Shelton',              'plays',                'Left'),
    ('Ben Shelton',              'backhand',             'Two-Handed'),
    ('Ben Shelton',              'country',              'USA'),
    ('Ben Shelton',              'grand_slam_titles',    '0'),
    ('Ben Shelton',              'career_high_ranking',  '5'),
    ('Ben Shelton',              'turned_pro_year',      '2022'),
    ('Ben Shelton',              'career_titles',        '5'),

    ('Tommy Paul',               'active_status',        'Active'),
    ('Tommy Paul',               'plays',                'Right'),
    ('Tommy Paul',               'backhand',             'Two-Handed'),
    ('Tommy Paul',               'country',              'USA'),
    ('Tommy Paul',               'grand_slam_titles',    '0'),
    ('Tommy Paul',               'career_high_ranking',  '8'),
    ('Tommy Paul',               'turned_pro_year',      '2015'),
    ('Tommy Paul',               'career_titles',        '5'),

    ('Ugo Humbert',              'active_status',        'Active'),
    ('Ugo Humbert',              'plays',                'Left'),
    ('Ugo Humbert',              'backhand',             'Two-Handed'),
    ('Ugo Humbert',              'country',              'France'),
    ('Ugo Humbert',              'grand_slam_titles',    '0'),
    ('Ugo Humbert',              'career_high_ranking',  '13'),
    ('Ugo Humbert',              'turned_pro_year',      '2016'),
    ('Ugo Humbert',              'career_titles',        '7'),

    ('Alex de Minaur',           'active_status',        'Active'),
    ('Alex de Minaur',           'plays',                'Right'),
    ('Alex de Minaur',           'backhand',             'Two-Handed'),
    ('Alex de Minaur',           'country',              'Australia'),
    ('Alex de Minaur',           'grand_slam_titles',    '0'),
    ('Alex de Minaur',           'career_high_ranking',  '6'),
    ('Alex de Minaur',           'turned_pro_year',      '2015'),
    ('Alex de Minaur',           'career_titles',        '11'),

    ('Karen Khachanov',          'active_status',        'Active'),
    ('Karen Khachanov',          'plays',                'Right'),
    ('Karen Khachanov',          'backhand',             'Two-Handed'),
    ('Karen Khachanov',          'country',              'Russia'),
    ('Karen Khachanov',          'grand_slam_titles',    '0'),
    ('Karen Khachanov',          'career_high_ranking',  '8'),
    ('Karen Khachanov',          'turned_pro_year',      '2013'),
    ('Karen Khachanov',          'career_titles',        '7'),

    ('Hubert Hurkacz',           'active_status',        'Active'),
    ('Hubert Hurkacz',           'plays',                'Right'),
    ('Hubert Hurkacz',           'backhand',             'Two-Handed'),
    ('Hubert Hurkacz',           'country',              'Poland'),
    ('Hubert Hurkacz',           'grand_slam_titles',    '0'),
    ('Hubert Hurkacz',           'career_high_ranking',  '6'),
    ('Hubert Hurkacz',           'turned_pro_year',      '2015'),
    ('Hubert Hurkacz',           'career_titles',        '8'),

    ('Felix Auger-Aliassime',    'active_status',        'Active'),
    ('Felix Auger-Aliassime',    'plays',                'Right'),
    ('Felix Auger-Aliassime',    'backhand',             'Two-Handed'),
    ('Felix Auger-Aliassime',    'country',              'Canada'),
    ('Felix Auger-Aliassime',    'grand_slam_titles',    '0'),
    ('Felix Auger-Aliassime',    'career_high_ranking',  '4'),
    ('Felix Auger-Aliassime',    'turned_pro_year',      '2017'),
    ('Felix Auger-Aliassime',    'career_titles',        '9'),

    ('Sebastian Korda',          'active_status',        'Active'),
    ('Sebastian Korda',          'plays',                'Right'),
    ('Sebastian Korda',          'backhand',             'Two-Handed'),
    ('Sebastian Korda',          'country',              'USA'),
    ('Sebastian Korda',          'grand_slam_titles',    '0'),
    ('Sebastian Korda',          'career_high_ranking',  '15'),
    ('Sebastian Korda',          'turned_pro_year',      '2018'),
    ('Sebastian Korda',          'career_titles',        '3'),

    ('Francisco Cerundolo',      'active_status',        'Active'),
    ('Francisco Cerundolo',      'plays',                'Right'),
    ('Francisco Cerundolo',      'backhand',             'Two-Handed'),
    ('Francisco Cerundolo',      'country',              'Argentina'),
    ('Francisco Cerundolo',      'grand_slam_titles',    '0'),
    ('Francisco Cerundolo',      'career_high_ranking',  '18'),
    ('Francisco Cerundolo',      'turned_pro_year',      '2018'),
    ('Francisco Cerundolo',      'career_titles',        '4'),

    ('Alexander Bublik',         'active_status',        'Active'),
    ('Alexander Bublik',         'plays',                'Right'),
    ('Alexander Bublik',         'backhand',             'Two-Handed'),
    ('Alexander Bublik',         'country',              'Kazakhstan'),
    ('Alexander Bublik',         'grand_slam_titles',    '0'),
    ('Alexander Bublik',         'career_high_ranking',  '10'),
    ('Alexander Bublik',         'turned_pro_year',      '2016'),
    ('Alexander Bublik',         'career_titles',        '9'),

    ('Flavio Cobolli',           'active_status',        'Active'),
    ('Flavio Cobolli',           'plays',                'Right'),
    ('Flavio Cobolli',           'backhand',             'Two-Handed'),
    ('Flavio Cobolli',           'country',              'Italy'),
    ('Flavio Cobolli',           'grand_slam_titles',    '0'),
    ('Flavio Cobolli',           'career_high_ranking',  '12'),
    ('Flavio Cobolli',           'turned_pro_year',      '2020'),
    ('Flavio Cobolli',           'career_titles',        '3'),

    ('Jack Draper',              'active_status',        'Active'),
    ('Jack Draper',              'plays',                'Left'),
    ('Jack Draper',              'backhand',             'Two-Handed'),
    ('Jack Draper',              'country',              'United Kingdom'),
    ('Jack Draper',              'grand_slam_titles',    '0'),
    ('Jack Draper',              'career_high_ranking',  '4'),
    ('Jack Draper',              'turned_pro_year',      '2018'),
    ('Jack Draper',              'career_titles',        '3'),

    ('Juan Martín del Potro',    'active_status',        'Retired'),
    ('Juan Martín del Potro',    'plays',                'Right'),
    ('Juan Martín del Potro',    'backhand',             'Two-Handed'),
    ('Juan Martín del Potro',    'country',              'Argentina'),
    ('Juan Martín del Potro',    'grand_slam_titles',    '1'),
    ('Juan Martín del Potro',    'career_high_ranking',  '3'),
    ('Juan Martín del Potro',    'turned_pro_year',      '2005'),
    ('Juan Martín del Potro',    'career_titles',        '22'),

    ('Marin Čilić',              'active_status',        'Active'),
    ('Marin Čilić',              'plays',                'Right'),
    ('Marin Čilić',              'backhand',             'Two-Handed'),
    ('Marin Čilić',              'country',              'Croatia'),
    ('Marin Čilić',              'grand_slam_titles',    '1'),
    ('Marin Čilić',              'career_high_ranking',  '3'),
    ('Marin Čilić',              'turned_pro_year',      '2005'),
    ('Marin Čilić',              'career_titles',        '20'),

    ('Kei Nishikori',            'active_status',        'Active'),
    ('Kei Nishikori',            'plays',                'Right'),
    ('Kei Nishikori',            'backhand',             'Two-Handed'),
    ('Kei Nishikori',            'country',              'Japan'),
    ('Kei Nishikori',            'grand_slam_titles',    '0'),
    ('Kei Nishikori',            'career_high_ranking',  '4'),
    ('Kei Nishikori',            'turned_pro_year',      '2007'),
    ('Kei Nishikori',            'career_titles',        '12'),

    ('Gaël Monfils',             'active_status',        'Active'),
    ('Gaël Monfils',             'plays',                'Right'),
    ('Gaël Monfils',             'backhand',             'Two-Handed'),
    ('Gaël Monfils',             'country',              'France'),
    ('Gaël Monfils',             'grand_slam_titles',    '0'),
    ('Gaël Monfils',             'career_high_ranking',  '6'),
    ('Gaël Monfils',             'turned_pro_year',      '2004'),
    ('Gaël Monfils',             'career_titles',        '13'),

    ('Tomáš Berdych',            'active_status',        'Active'),
    ('Tomáš Berdych',            'plays',                'Right'),
    ('Tomáš Berdych',            'backhand',             'Two-Handed'),
    ('Tomáš Berdych',            'country',              'Czech Republic'),
    ('Tomáš Berdych',            'grand_slam_titles',    '0'),
    ('Tomáš Berdych',            'career_high_ranking',  '4'),
    ('Tomáš Berdych',            'turned_pro_year',      '2002'),
    ('Tomáš Berdych',            'career_titles',        '13'),

    ('Richard Gasquet',          'active_status',        'Active'),
    ('Richard Gasquet',          'plays',                'Right'),
    ('Richard Gasquet',          'backhand',             'One-Handed'),
    ('Richard Gasquet',          'country',              'France'),
    ('Richard Gasquet',          'grand_slam_titles',    '0'),
    ('Richard Gasquet',          'career_high_ranking',  '7'),
    ('Richard Gasquet',          'turned_pro_year',      '2002'),
    ('Richard Gasquet',          'career_titles',        '16'),

    ('Nicolás Almagro',          'active_status',        'Active'),
    ('Nicolás Almagro',          'plays',                'Right'),
    ('Nicolás Almagro',          'backhand',             'One-Handed'),
    ('Nicolás Almagro',          'country',              'Spain'),
    ('Nicolás Almagro',          'grand_slam_titles',    '0'),
    ('Nicolás Almagro',          'career_high_ranking',  '9'),
    ('Nicolás Almagro',          'turned_pro_year',      '2001'),
    ('Nicolás Almagro',          'career_titles',        '13'),

    ('Ivan Lendl',               'active_status',        'Retired'),
    ('Ivan Lendl',               'plays',                'Right'),
    ('Ivan Lendl',               'backhand',             'Two-Handed'),
    ('Ivan Lendl',               'country',              'Czech Republic'),
    ('Ivan Lendl',               'grand_slam_titles',    '8'),
    ('Ivan Lendl',               'career_high_ranking',  '1'),
    ('Ivan Lendl',               'turned_pro_year',      '1978'),
    ('Ivan Lendl',               'career_titles',        '94'),

    ('Björn Borg',               'active_status',        'Retired'),
    ('Björn Borg',               'plays',                'Right'),
    ('Björn Borg',               'backhand',             'Two-Handed'),
    ('Björn Borg',               'country',              'Sweden'),
    ('Björn Borg',               'grand_slam_titles',    '11'),
    ('Björn Borg',               'career_high_ranking',  '1'),
    ('Björn Borg',               'turned_pro_year',      '1973'),
    ('Björn Borg',               'career_titles',        '64'),

    ('Jimmy Connors',            'active_status',        'Retired'),
    ('Jimmy Connors',            'plays',                'Left'),
    ('Jimmy Connors',            'backhand',             'Two-Handed'),
    ('Jimmy Connors',            'country',              'USA'),
    ('Jimmy Connors',            'grand_slam_titles',    '8'),
    ('Jimmy Connors',            'career_high_ranking',  '1'),
    ('Jimmy Connors',            'turned_pro_year',      '1972'),
    ('Jimmy Connors',            'career_titles',        '109'),

    ('Guillermo Vilas',          'active_status',        'Retired'),
    ('Guillermo Vilas',          'plays',                'Left'),
    ('Guillermo Vilas',          'backhand',             'Two-Handed'),
    ('Guillermo Vilas',          'country',              'Argentina'),
    ('Guillermo Vilas',          'grand_slam_titles',    '4'),
    ('Guillermo Vilas',          'career_high_ranking',  '2'),
    ('Guillermo Vilas',          'turned_pro_year',      '1969'),
    ('Guillermo Vilas',          'career_titles',        '62')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";