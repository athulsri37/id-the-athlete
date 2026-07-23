-- Global (sport-agnostic) application settings. ON CONFLICT DO NOTHING so
-- re-running the seed tool never clobbers a value an operator has since
-- changed by hand (e.g. flipping CountryClosenessEnabled off, or setting
-- ActiveTheme) -- these rows are only meant to establish a default the
-- first time, not to be re-asserted on every seed run.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CountryClosenessEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;