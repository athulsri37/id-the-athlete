-- Global (sport-agnostic) application settings. ON CONFLICT DO NOTHING so
-- re-running the seed tool never clobbers a value an operator has since
-- changed by hand (e.g. flipping CountryClosenessEnabled off, or setting
-- ActiveTheme) -- these rows are only meant to establish a default the
-- first time, not to be re-asserted on every seed run.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CountryClosenessEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketRoleClueEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;

-- Cricket numeric-closeness thresholds: percent-of-actual-value plus a
-- floor, per attribute (see GameService.ComputeCricketNumericCloseness).
-- Read fresh from AppSettings on every guess, not cached at startup, so an
-- operator can retune these live via SQL with no redeploy.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketMatchesClosenessPercent', '15')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketMatchesClosenessFloor', '20')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketRunsClosenessPercent', '15')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketRunsClosenessFloor', '500')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketWicketsClosenessPercent', '15')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketWicketsClosenessFloor', '15')
ON CONFLICT ("Key") DO NOTHING;