-- Global (sport-agnostic) application settings. ON CONFLICT DO NOTHING so
-- re-running the seed tool never clobbers a value an operator has since
-- changed by hand (e.g. flipping CountryClosenessEnabled off, or setting
-- ActiveTheme) -- these rows are only meant to establish a default the
-- first time, not to be re-asserted on every seed run.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CountryClosenessEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;

-- AI trivia blurb on game over, off by default. Turned on from the admin
-- tool's Settings tab, and it also needs an Anthropic API key configured.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('AiTriviaEnabled', 'false')
ON CONFLICT ("Key") DO NOTHING;

-- Cricket numeric-closeness thresholds: percent-of-actual-value plus a
-- floor, per attribute (see NumericClosenessEvaluator).
-- Read fresh from AppSettings on every guess, not cached at startup, so an
-- operator can retune these live from the admin tool with no redeploy.
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

-- Cricket categorical closeness (Role, Bowling Style), each independently
-- toggleable -- when false, that clue reverts to plain match/miss with no
-- closeness check at all, same pattern as CountryClosenessEnabled.
INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketRoleClosenessEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('CricketBowlingStyleClosenessEnabled', 'true')
ON CONFLICT ("Key") DO NOTHING;