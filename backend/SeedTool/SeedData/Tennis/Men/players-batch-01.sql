-- Tennis (men's) players, batch 1 of 20 (players 1-20 in this app's
-- roster). Batches are 50 players each going forward; this first batch
-- is smaller since it's simply the original 20-player sample set migrated
-- into this format unchanged.
--
-- Safe to re-run: Players upserts on the unique Name constraint, and
-- PlayerAttributeValues upserts on the existing unique
-- (PlayerId, AttributeDefinitionId) constraint, both added/confirmed in
-- migration AddPlayerNameAndAttributeDefinitionUniqueConstraints.

INSERT INTO "Players" ("SportId", "Name")
SELECT s."Id", v."Name"
FROM "Sports" s
CROSS JOIN (VALUES
    ('Pete Sampras'),
    ('Andre Agassi'),
    ('Boris Becker'),
    ('John McEnroe'),
    ('Roger Federer'),
    ('Rafael Nadal'),
    ('Novak Djokovic'),
    ('Andy Murray'),
    ('Stan Wawrinka'),
    ('Lleyton Hewitt'),
    ('David Ferrer'),
    ('Carlos Alcaraz'),
    ('Jannik Sinner'),
    ('Daniil Medvedev'),
    ('Alexander Zverev'),
    ('Stefanos Tsitsipas'),
    ('Casper Ruud'),
    ('Taylor Fritz'),
    ('Holger Rune'),
    ('Andrey Rublev')
) AS v("Name")
WHERE s."Slug" = 'tennis'
ON CONFLICT ("Name") DO UPDATE SET
    "SportId" = EXCLUDED."SportId";

-- One row per (player, attribute) pair. PlayerId/AttributeDefinitionId are
-- resolved by name/key lookup rather than hardcoded ids, so this file has
-- no dependency on insertion order or id values.
INSERT INTO "PlayerAttributeValues" ("PlayerId", "AttributeDefinitionId", "Value")
SELECT p."Id", ad."Id", v."Value"
FROM (VALUES
    -- Player,          AttrKey,                Value
    ('Pete Sampras',    'active_status',        'Retired'),
    ('Pete Sampras',    'plays',                'Right'),
    ('Pete Sampras',    'backhand',             'One-Handed'),
    ('Pete Sampras',    'country',              'USA'),
    ('Pete Sampras',    'grand_slam_titles',    '14'),
    ('Pete Sampras',    'career_high_ranking',  '1'),
    ('Pete Sampras',    'turned_pro_year',      '1988'),
    ('Pete Sampras',    'career_titles',        '64'),

    ('Andre Agassi',    'active_status',        'Retired'),
    ('Andre Agassi',    'plays',                'Right'),
    ('Andre Agassi',    'backhand',             'Two-Handed'),
    ('Andre Agassi',    'country',              'USA'),
    ('Andre Agassi',    'grand_slam_titles',    '8'),
    ('Andre Agassi',    'career_high_ranking',  '1'),
    ('Andre Agassi',    'turned_pro_year',      '1986'),
    ('Andre Agassi',    'career_titles',        '60'),

    ('Boris Becker',    'active_status',        'Retired'),
    ('Boris Becker',    'plays',                'Right'),
    ('Boris Becker',    'backhand',             'One-Handed'),
    ('Boris Becker',    'country',              'Germany'),
    ('Boris Becker',    'grand_slam_titles',    '6'),
    ('Boris Becker',    'career_high_ranking',  '1'),
    ('Boris Becker',    'turned_pro_year',      '1984'),
    ('Boris Becker',    'career_titles',        '49'),

    ('John McEnroe',    'active_status',        'Retired'),
    ('John McEnroe',    'plays',                'Left'),
    ('John McEnroe',    'backhand',             'One-Handed'),
    ('John McEnroe',    'country',              'USA'),
    ('John McEnroe',    'grand_slam_titles',    '7'),
    ('John McEnroe',    'career_high_ranking',  '1'),
    ('John McEnroe',    'turned_pro_year',      '1978'),
    ('John McEnroe',    'career_titles',        '77'),

    ('Roger Federer',   'active_status',        'Retired'),
    ('Roger Federer',   'plays',                'Right'),
    ('Roger Federer',   'backhand',             'One-Handed'),
    ('Roger Federer',   'country',              'Switzerland'),
    ('Roger Federer',   'grand_slam_titles',    '20'),
    ('Roger Federer',   'career_high_ranking',  '1'),
    ('Roger Federer',   'turned_pro_year',      '1998'),
    ('Roger Federer',   'career_titles',        '103'),

    ('Rafael Nadal',    'active_status',        'Retired'),
    ('Rafael Nadal',    'plays',                'Left'),
    ('Rafael Nadal',    'backhand',             'Two-Handed'),
    ('Rafael Nadal',    'country',              'Spain'),
    ('Rafael Nadal',    'grand_slam_titles',    '22'),
    ('Rafael Nadal',    'career_high_ranking',  '1'),
    ('Rafael Nadal',    'turned_pro_year',      '2001'),
    ('Rafael Nadal',    'career_titles',        '92'),

    ('Novak Djokovic',  'active_status',        'Active'),
    ('Novak Djokovic',  'plays',                'Right'),
    ('Novak Djokovic',  'backhand',             'Two-Handed'),
    ('Novak Djokovic',  'country',              'Serbia'),
    ('Novak Djokovic',  'grand_slam_titles',    '24'),
    ('Novak Djokovic',  'career_high_ranking',  '1'),
    ('Novak Djokovic',  'turned_pro_year',      '2003'),
    ('Novak Djokovic',  'career_titles',        '100'),

    ('Andy Murray',     'active_status',        'Active'),
    ('Andy Murray',     'plays',                'Right'),
    ('Andy Murray',     'backhand',             'Two-Handed'),
    ('Andy Murray',     'country',              'United Kingdom'),
    ('Andy Murray',     'grand_slam_titles',    '3'),
    ('Andy Murray',     'career_high_ranking',  '1'),
    ('Andy Murray',     'turned_pro_year',      '2005'),
    ('Andy Murray',     'career_titles',        '46'),

    ('Stan Wawrinka',   'active_status',        'Active'),
    ('Stan Wawrinka',   'plays',                'Right'),
    ('Stan Wawrinka',   'backhand',             'One-Handed'),
    ('Stan Wawrinka',   'country',              'Switzerland'),
    ('Stan Wawrinka',   'grand_slam_titles',    '3'),
    ('Stan Wawrinka',   'career_high_ranking',  '3'),
    ('Stan Wawrinka',   'turned_pro_year',      '2002'),
    ('Stan Wawrinka',   'career_titles',        '16'),

    ('Lleyton Hewitt',  'active_status',        'Retired'),
    ('Lleyton Hewitt',  'plays',                'Right'),
    ('Lleyton Hewitt',  'backhand',             'Two-Handed'),
    ('Lleyton Hewitt',  'country',              'Australia'),
    ('Lleyton Hewitt',  'grand_slam_titles',    '2'),
    ('Lleyton Hewitt',  'career_high_ranking',  '1'),
    ('Lleyton Hewitt',  'turned_pro_year',      '1998'),
    ('Lleyton Hewitt',  'career_titles',        '30'),

    ('David Ferrer',    'active_status',        'Retired'),
    ('David Ferrer',    'plays',                'Right'),
    ('David Ferrer',    'backhand',             'Two-Handed'),
    ('David Ferrer',    'country',              'Spain'),
    ('David Ferrer',    'grand_slam_titles',    '0'),
    ('David Ferrer',    'career_high_ranking',  '3'),
    ('David Ferrer',    'turned_pro_year',      '2000'),
    ('David Ferrer',    'career_titles',        '27'),

    ('Carlos Alcaraz',  'active_status',        'Active'),
    ('Carlos Alcaraz',  'plays',                'Right'),
    ('Carlos Alcaraz',  'backhand',             'Two-Handed'),
    ('Carlos Alcaraz',  'country',              'Spain'),
    ('Carlos Alcaraz',  'grand_slam_titles',    '7'),
    ('Carlos Alcaraz',  'career_high_ranking',  '1'),
    ('Carlos Alcaraz',  'turned_pro_year',      '2018'),
    ('Carlos Alcaraz',  'career_titles',        '22'),

    ('Jannik Sinner',   'active_status',        'Active'),
    ('Jannik Sinner',   'plays',                'Right'),
    ('Jannik Sinner',   'backhand',             'Two-Handed'),
    ('Jannik Sinner',   'country',              'Italy'),
    ('Jannik Sinner',   'grand_slam_titles',    '5'),
    ('Jannik Sinner',   'career_high_ranking',  '1'),
    ('Jannik Sinner',   'turned_pro_year',      '2018'),
    ('Jannik Sinner',   'career_titles',        '20'),

    ('Daniil Medvedev', 'active_status',        'Active'),
    ('Daniil Medvedev', 'plays',                'Right'),
    ('Daniil Medvedev', 'backhand',             'Two-Handed'),
    ('Daniil Medvedev', 'country',              'Russia'),
    ('Daniil Medvedev', 'grand_slam_titles',    '1'),
    ('Daniil Medvedev', 'career_high_ranking',  '1'),
    ('Daniil Medvedev', 'turned_pro_year',      '2014'),
    ('Daniil Medvedev', 'career_titles',        '20'),

    ('Alexander Zverev', 'active_status',       'Active'),
    ('Alexander Zverev', 'plays',               'Right'),
    ('Alexander Zverev', 'backhand',            'Two-Handed'),
    ('Alexander Zverev', 'country',             'Germany'),
    ('Alexander Zverev', 'grand_slam_titles',   '0'),
    ('Alexander Zverev', 'career_high_ranking', '2'),
    ('Alexander Zverev', 'turned_pro_year',     '2013'),
    ('Alexander Zverev', 'career_titles',       '24'),

    ('Stefanos Tsitsipas', 'active_status',        'Active'),
    ('Stefanos Tsitsipas', 'plays',                'Right'),
    ('Stefanos Tsitsipas', 'backhand',             'One-Handed'),
    ('Stefanos Tsitsipas', 'country',              'Greece'),
    ('Stefanos Tsitsipas', 'grand_slam_titles',    '0'),
    ('Stefanos Tsitsipas', 'career_high_ranking',  '3'),
    ('Stefanos Tsitsipas', 'turned_pro_year',      '2016'),
    ('Stefanos Tsitsipas', 'career_titles',        '12'),

    ('Casper Ruud',      'active_status',       'Active'),
    ('Casper Ruud',      'plays',               'Right'),
    ('Casper Ruud',      'backhand',            'Two-Handed'),
    ('Casper Ruud',      'country',             'Norway'),
    ('Casper Ruud',      'grand_slam_titles',   '0'),
    ('Casper Ruud',      'career_high_ranking', '2'),
    ('Casper Ruud',      'turned_pro_year',     '2015'),
    ('Casper Ruud',      'career_titles',       '10'),

    ('Taylor Fritz',     'active_status',       'Active'),
    ('Taylor Fritz',     'plays',               'Right'),
    ('Taylor Fritz',     'backhand',            'Two-Handed'),
    ('Taylor Fritz',     'country',             'USA'),
    ('Taylor Fritz',     'grand_slam_titles',   '0'),
    ('Taylor Fritz',     'career_high_ranking', '4'),
    ('Taylor Fritz',     'turned_pro_year',     '2015'),
    ('Taylor Fritz',     'career_titles',       '9'),

    ('Holger Rune',      'active_status',       'Active'),
    ('Holger Rune',      'plays',               'Right'),
    ('Holger Rune',      'backhand',            'Two-Handed'),
    ('Holger Rune',      'country',             'Denmark'),
    ('Holger Rune',      'grand_slam_titles',   '0'),
    ('Holger Rune',      'career_high_ranking', '4'),
    ('Holger Rune',      'turned_pro_year',     '2019'),
    ('Holger Rune',      'career_titles',       '6'),

    ('Andrey Rublev',    'active_status',       'Active'),
    ('Andrey Rublev',    'plays',               'Right'),
    ('Andrey Rublev',    'backhand',            'Two-Handed'),
    ('Andrey Rublev',    'country',             'Russia'),
    ('Andrey Rublev',    'grand_slam_titles',   '0'),
    ('Andrey Rublev',    'career_high_ranking', '5'),
    ('Andrey Rublev',    'turned_pro_year',     '2014'),
    ('Andrey Rublev',    'career_titles',       '17')
) AS v("PlayerName", "AttrKey", "Value")
JOIN "Players" p ON p."Name" = v."PlayerName"
JOIN "AttributeDefinitions" ad ON ad."Key" = v."AttrKey" AND ad."SportId" = p."SportId"
ON CONFLICT ("PlayerId", "AttributeDefinitionId") DO UPDATE SET
    "Value" = EXCLUDED."Value";