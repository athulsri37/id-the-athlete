-- Tennis (men's) players, batch 6 (25 players, bringing the roster to 145
-- total: batch 1's 20 + batches 2-6's 25 each). Same upsert pattern as
-- prior batches: Players upserts on the unique Name constraint,
-- PlayerAttributeValues upserts on the existing unique (PlayerId,
-- AttributeDefinitionId) constraint. No overrides in this batch -- every
-- row is IsOverridden = false, DifficultyOverride = NULL.
--
-- Note: Dan Evans', Fred Perry's, and Kyle Edmund's country is normalized
-- to "United Kingdom" (not "UK"), matching the same normalization already
-- applied to Jack Draper (batch 2) and Cameron Norrie (batch 5), to keep a
-- single consistent value for this country.

INSERT INTO "Players" ("SportId", "Name", "IsOverridden", "DifficultyOverride")
SELECT s."Id", v."Name", v."IsOverridden", v."DifficultyOverride"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Dan Evans',                   false, NULL),
    ('Jordan Thompson',             false, NULL),
    ('Yoshihito Nishioka',          false, NULL),
    ('Zhang Zhizhen',               false, NULL),
    ('Tomáš Macháč',                false, NULL),
    ('Dustin Brown',                false, NULL),
    ('Nicolas Mahut',               false, NULL),
    ('Lukáš Rosol',                 false, NULL),
    ('Steve Darcis',                false, NULL),
    ('Marton Fucsovics',            false, NULL),
    ('Thanasi Kokkinakis',          false, NULL),
    ('Sebastián Báez',              false, NULL),
    ('Giovanni Mpetshi Perricard',  false, NULL),
    ('Alejandro Tabilo',            false, NULL),
    ('Damir Džumhur',               false, NULL),
    ('Nicolás Jarry',               false, NULL),
    ('Jaume Munar',                 false, NULL),
    ('Miomir Kecmanović',           false, NULL),
    ('Pablo Carreño Busta',         false, NULL),

    ('Fred Perry',                  false, NULL),
    ('Ken Rosewall',                false, NULL),
    ('Roy Emerson',                 false, NULL),
    ('Vitas Gerulaitis',            false, NULL),
    ('Manuel Orantes',              false, NULL),
    ('Kyle Edmund',                 false, NULL)
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
    ('Dan Evans',                     'active_status',        'Retired'),
    ('Dan Evans',                     'plays',                'Right'),
    ('Dan Evans',                     'backhand',              'One-Handed'),
    ('Dan Evans',                     'country',              'United Kingdom'),
    ('Dan Evans',                     'grand_slam_titles',    '0'),
    ('Dan Evans',                     'career_high_ranking',  '21'),
    ('Dan Evans',                     'turned_pro_year',      '2006'),
    ('Dan Evans',                     'career_titles',        '2'),

    ('Jordan Thompson',               'active_status',        'Active'),
    ('Jordan Thompson',               'plays',                'Right'),
    ('Jordan Thompson',               'backhand',             'Two-Handed'),
    ('Jordan Thompson',               'country',              'Australia'),
    ('Jordan Thompson',               'grand_slam_titles',    '0'),
    ('Jordan Thompson',               'career_high_ranking',  '26'),
    ('Jordan Thompson',               'turned_pro_year',      '2013'),
    ('Jordan Thompson',               'career_titles',        '1'),

    ('Yoshihito Nishioka',            'active_status',        'Active'),
    ('Yoshihito Nishioka',            'plays',                'Left'),
    ('Yoshihito Nishioka',            'backhand',             'Two-Handed'),
    ('Yoshihito Nishioka',            'country',              'Japan'),
    ('Yoshihito Nishioka',            'grand_slam_titles',    '0'),
    ('Yoshihito Nishioka',            'career_high_ranking',  '24'),
    ('Yoshihito Nishioka',            'turned_pro_year',      '2014'),
    ('Yoshihito Nishioka',            'career_titles',        '3'),

    ('Zhang Zhizhen',                 'active_status',        'Active'),
    ('Zhang Zhizhen',                 'plays',                'Right'),
    ('Zhang Zhizhen',                 'backhand',             'Two-Handed'),
    ('Zhang Zhizhen',                 'country',              'China'),
    ('Zhang Zhizhen',                 'grand_slam_titles',    '0'),
    ('Zhang Zhizhen',                 'career_high_ranking',  '31'),
    ('Zhang Zhizhen',                 'turned_pro_year',      '2012'),
    ('Zhang Zhizhen',                 'career_titles',        '0'),

    ('Tomáš Macháč',                  'active_status',        'Active'),
    ('Tomáš Macháč',                  'plays',                'Right'),
    ('Tomáš Macháč',                  'backhand',             'Two-Handed'),
    ('Tomáš Macháč',                  'country',              'Czech Republic'),
    ('Tomáš Macháč',                  'grand_slam_titles',    '0'),
    ('Tomáš Macháč',                  'career_high_ranking',  '20'),
    ('Tomáš Macháč',                  'turned_pro_year',      '2017'),
    ('Tomáš Macháč',                  'career_titles',        '2'),

    ('Dustin Brown',                  'active_status',        'Retired'),
    ('Dustin Brown',                  'plays',                'Right'),
    ('Dustin Brown',                  'backhand',             'Two-Handed'),
    ('Dustin Brown',                  'country',              'Germany'),
    ('Dustin Brown',                  'grand_slam_titles',    '0'),
    ('Dustin Brown',                  'career_high_ranking',  '64'),
    ('Dustin Brown',                  'turned_pro_year',      '2002'),
    ('Dustin Brown',                  'career_titles',        '0'),

    ('Nicolas Mahut',                 'active_status',        'Retired'),
    ('Nicolas Mahut',                 'plays',                'Right'),
    ('Nicolas Mahut',                 'backhand',             'One-Handed'),
    ('Nicolas Mahut',                 'country',              'France'),
    ('Nicolas Mahut',                 'grand_slam_titles',    '0'),
    ('Nicolas Mahut',                 'career_high_ranking',  '37'),
    ('Nicolas Mahut',                 'turned_pro_year',      '2000'),
    ('Nicolas Mahut',                 'career_titles',        '4'),

    ('Lukáš Rosol',                   'active_status',        'Retired'),
    ('Lukáš Rosol',                   'plays',                'Right'),
    ('Lukáš Rosol',                   'backhand',             'Two-Handed'),
    ('Lukáš Rosol',                   'country',              'Czech Republic'),
    ('Lukáš Rosol',                   'grand_slam_titles',    '0'),
    ('Lukáš Rosol',                   'career_high_ranking',  '26'),
    ('Lukáš Rosol',                   'turned_pro_year',      '2004'),
    ('Lukáš Rosol',                   'career_titles',        '2'),

    ('Steve Darcis',                  'active_status',        'Retired'),
    ('Steve Darcis',                  'plays',                'Right'),
    ('Steve Darcis',                  'backhand',             'One-Handed'),
    ('Steve Darcis',                  'country',              'Belgium'),
    ('Steve Darcis',                  'grand_slam_titles',    '0'),
    ('Steve Darcis',                  'career_high_ranking',  '38'),
    ('Steve Darcis',                  'turned_pro_year',      '2003'),
    ('Steve Darcis',                  'career_titles',        '2'),

    ('Marton Fucsovics',              'active_status',        'Active'),
    ('Marton Fucsovics',              'plays',                'Right'),
    ('Marton Fucsovics',              'backhand',             'Two-Handed'),
    ('Marton Fucsovics',              'country',              'Hungary'),
    ('Marton Fucsovics',              'grand_slam_titles',    '0'),
    ('Marton Fucsovics',              'career_high_ranking',  '31'),
    ('Marton Fucsovics',              'turned_pro_year',      '2010'),
    ('Marton Fucsovics',              'career_titles',        '3'),

    ('Fred Perry',                    'active_status',        'Retired'),
    ('Fred Perry',                    'plays',                'Right'),
    ('Fred Perry',                    'backhand',             'One-Handed'),
    ('Fred Perry',                    'country',              'United Kingdom'),
    ('Fred Perry',                    'grand_slam_titles',    '8'),
    ('Fred Perry',                    'career_high_ranking',  '1'),
    ('Fred Perry',                    'turned_pro_year',      '1923'),
    ('Fred Perry',                    'career_titles',        '62'),

    ('Ken Rosewall',                  'active_status',        'Retired'),
    ('Ken Rosewall',                  'plays',                'Right'),
    ('Ken Rosewall',                  'backhand',             'One-Handed'),
    ('Ken Rosewall',                  'country',              'Australia'),
    ('Ken Rosewall',                  'grand_slam_titles',    '8'),
    ('Ken Rosewall',                  'career_high_ranking',  '1'),
    ('Ken Rosewall',                  'turned_pro_year',      '1956'),
    ('Ken Rosewall',                  'career_titles',        '40'),

    ('Roy Emerson',                   'active_status',        'Retired'),
    ('Roy Emerson',                   'plays',                'Right'),
    ('Roy Emerson',                   'backhand',             'One-Handed'),
    ('Roy Emerson',                   'country',              'Australia'),
    ('Roy Emerson',                   'grand_slam_titles',    '12'),
    ('Roy Emerson',                   'career_high_ranking',  '1'),
    ('Roy Emerson',                   'turned_pro_year',      '1968'),
    ('Roy Emerson',                   'career_titles',        '6'),

    ('Vitas Gerulaitis',               'active_status',        'Retired'),
    ('Vitas Gerulaitis',               'plays',                'Right'),
    ('Vitas Gerulaitis',               'backhand',             'One-Handed'),
    ('Vitas Gerulaitis',               'country',              'USA'),
    ('Vitas Gerulaitis',               'grand_slam_titles',    '1'),
    ('Vitas Gerulaitis',               'career_high_ranking',  '3'),
    ('Vitas Gerulaitis',               'turned_pro_year',      '1971'),
    ('Vitas Gerulaitis',               'career_titles',        '26'),

    ('Manuel Orantes',                 'active_status',        'Retired'),
    ('Manuel Orantes',                 'plays',                'Left'),
    ('Manuel Orantes',                 'backhand',             'One-Handed'),
    ('Manuel Orantes',                 'country',              'Spain'),
    ('Manuel Orantes',                 'grand_slam_titles',    '1'),
    ('Manuel Orantes',                 'career_high_ranking',  '2'),
    ('Manuel Orantes',                 'turned_pro_year',      '1968'),
    ('Manuel Orantes',                 'career_titles',        '36'),

    ('Kyle Edmund',                    'active_status',        'Retired'),
    ('Kyle Edmund',                    'plays',                'Right'),
    ('Kyle Edmund',                    'backhand',             'Two-Handed'),
    ('Kyle Edmund',                    'country',              'United Kingdom'),
    ('Kyle Edmund',                    'grand_slam_titles',    '0'),
    ('Kyle Edmund',                    'career_high_ranking',  '14'),
    ('Kyle Edmund',                    'turned_pro_year',      '2011'),
    ('Kyle Edmund',                    'career_titles',        '2'),

    ('Thanasi Kokkinakis',             'active_status',        'Active'),
    ('Thanasi Kokkinakis',             'plays',                'Right'),
    ('Thanasi Kokkinakis',             'backhand',             'Two-Handed'),
    ('Thanasi Kokkinakis',             'country',              'Australia'),
    ('Thanasi Kokkinakis',             'grand_slam_titles',    '0'),
    ('Thanasi Kokkinakis',             'career_high_ranking',  '65'),
    ('Thanasi Kokkinakis',             'turned_pro_year',      '2013'),
    ('Thanasi Kokkinakis',             'career_titles',        '1'),

    ('Sebastián Báez',                 'active_status',        'Active'),
    ('Sebastián Báez',                 'plays',                'Right'),
    ('Sebastián Báez',                 'backhand',             'Two-Handed'),
    ('Sebastián Báez',                 'country',              'Argentina'),
    ('Sebastián Báez',                 'grand_slam_titles',    '0'),
    ('Sebastián Báez',                 'career_high_ranking',  '18'),
    ('Sebastián Báez',                 'turned_pro_year',      '2018'),
    ('Sebastián Báez',                 'career_titles',        '7'),

    ('Giovanni Mpetshi Perricard',     'active_status',        'Active'),
    ('Giovanni Mpetshi Perricard',     'plays',                'Right'),
    ('Giovanni Mpetshi Perricard',     'backhand',             'One-Handed'),
    ('Giovanni Mpetshi Perricard',     'country',              'France'),
    ('Giovanni Mpetshi Perricard',     'grand_slam_titles',    '0'),
    ('Giovanni Mpetshi Perricard',     'career_high_ranking',  '29'),
    ('Giovanni Mpetshi Perricard',     'turned_pro_year',      '2021'),
    ('Giovanni Mpetshi Perricard',     'career_titles',        '2'),

    ('Alejandro Tabilo',               'active_status',        'Active'),
    ('Alejandro Tabilo',               'plays',                'Left'),
    ('Alejandro Tabilo',               'backhand',             'Two-Handed'),
    ('Alejandro Tabilo',               'country',              'Chile'),
    ('Alejandro Tabilo',               'grand_slam_titles',    '0'),
    ('Alejandro Tabilo',               'career_high_ranking',  '19'),
    ('Alejandro Tabilo',               'turned_pro_year',      '2015'),
    ('Alejandro Tabilo',               'career_titles',        '3'),

    ('Damir Džumhur',                  'active_status',        'Active'),
    ('Damir Džumhur',                  'plays',                'Right'),
    ('Damir Džumhur',                  'backhand',             'Two-Handed'),
    ('Damir Džumhur',                  'country',              'Bosnia and Herzegovina'),
    ('Damir Džumhur',                  'grand_slam_titles',    '0'),
    ('Damir Džumhur',                  'career_high_ranking',  '23'),
    ('Damir Džumhur',                  'turned_pro_year',      '2011'),
    ('Damir Džumhur',                  'career_titles',        '3'),

    ('Nicolás Jarry',                  'active_status',        'Active'),
    ('Nicolás Jarry',                  'plays',                'Right'),
    ('Nicolás Jarry',                  'backhand',             'Two-Handed'),
    ('Nicolás Jarry',                  'country',              'Chile'),
    ('Nicolás Jarry',                  'grand_slam_titles',    '0'),
    ('Nicolás Jarry',                  'career_high_ranking',  '16'),
    ('Nicolás Jarry',                  'turned_pro_year',      '2014'),
    ('Nicolás Jarry',                  'career_titles',        '3'),

    ('Jaume Munar',                    'active_status',        'Active'),
    ('Jaume Munar',                    'plays',                'Right'),
    ('Jaume Munar',                    'backhand',             'Two-Handed'),
    ('Jaume Munar',                    'country',              'Spain'),
    ('Jaume Munar',                    'grand_slam_titles',    '0'),
    ('Jaume Munar',                    'career_high_ranking',  '33'),
    ('Jaume Munar',                    'turned_pro_year',      '2014'),
    ('Jaume Munar',                    'career_titles',        '0'),

    ('Miomir Kecmanović',              'active_status',        'Active'),
    ('Miomir Kecmanović',              'plays',                'Right'),
    ('Miomir Kecmanović',              'backhand',             'Two-Handed'),
    ('Miomir Kecmanović',              'country',              'Serbia'),
    ('Miomir Kecmanović',              'grand_slam_titles',    '0'),
    ('Miomir Kecmanović',              'career_high_ranking',  '27'),
    ('Miomir Kecmanović',              'turned_pro_year',      '2017'),
    ('Miomir Kecmanović',              'career_titles',        '2'),

    ('Pablo Carreño Busta',            'active_status',        'Active'),
    ('Pablo Carreño Busta',            'plays',                'Right'),
    ('Pablo Carreño Busta',            'backhand',             'Two-Handed'),
    ('Pablo Carreño Busta',            'country',              'Spain'),
    ('Pablo Carreño Busta',            'grand_slam_titles',    '0'),
    ('Pablo Carreño Busta',            'career_high_ranking',  '10'),
    ('Pablo Carreño Busta',            'turned_pro_year',      '2009'),
    ('Pablo Carreño Busta',            'career_titles',        '7')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";