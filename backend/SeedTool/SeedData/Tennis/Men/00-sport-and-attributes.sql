-- Tennis (men's) sport row and its attribute definitions.
-- Safe to re-run: both upserts are keyed on unique constraints
-- (Sports.Slug, AttributeDefinitions(SportId, Key)) added in migration
-- AddPlayerNameAndAttributeDefinitionUniqueConstraints.

INSERT INTO "Sports" ("Name", "Slug")
VALUES ('Tennis', 'tennis')
ON CONFLICT ("Slug") DO UPDATE SET
    "Name" = EXCLUDED."Name";

INSERT INTO "AttributeDefinitions" ("SportId", "Key", "Label", "Type", "DisplayOrder")
SELECT s."Id", v."Key", v."Label", v."Type", v."DisplayOrder"
FROM "Sports" s
CROSS JOIN (VALUES
    -- Key,                   Label,               Type (0=Categorical, 1=Numeric), DisplayOrder
    ('active_status',         'Status',            0, 0),
    ('plays',                 'Plays',             0, 1),
    ('backhand',              'Backhand',          0, 2),
    ('country',                'Country',          0, 3),
    ('grand_slam_titles',      'Grand Slams',      1, 4),
    ('career_high_ranking',    'Career-High Rank', 1, 5),
    ('turned_pro_year',        'Turned Pro',       1, 6),
    ('career_titles',          'Career Titles',    1, 7)
) AS v("Key", "Label", "Type", "DisplayOrder")
WHERE s."Slug" = 'tennis'
ON CONFLICT ("SportId", "Key") DO UPDATE SET
    "Label" = EXCLUDED."Label",
    "Type" = EXCLUDED."Type",
    "DisplayOrder" = EXCLUDED."DisplayOrder";