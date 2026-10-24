"""Run from the tools folder: python -m unittest test_roster_validation"""

import unittest

from roster_validation import (
    CATEGORICAL,
    NUMERIC,
    AttributeSpec,
    describe_tiers,
    parse_tiers,
    prompt_countries,
    schema_mismatch,
    tier_warning,
    validate_player,
    write_confirmation,
)

SPECS = [
    AttributeSpec("country", CATEGORICAL, frozenset({"India", "United Kingdom", "New Zealand"})),
    AttributeSpec("role", CATEGORICAL, frozenset({"Batter", "Bowler", "Wicketkeeper-Batter"})),
    AttributeSpec("bowling_style", CATEGORICAL, frozenset({"Right-arm Pace", "Left-arm Spin", "Hasn't Bowled"})),
    AttributeSpec("combined_runs", NUMERIC),
    AttributeSpec("debut_year", NUMERIC),
]

VALID = {
    "name": "Test Player",
    "country": "India",
    "role": "Batter",
    "bowling_style": "Right-arm Pace",
    "combined_runs": "1234",
    "debut_year": 2015,
}


def player(**overrides):
    return {**VALID, **overrides}


class ValidatePlayerTests(unittest.TestCase):
    def test_valid_player_has_no_issues_and_clean_values(self):
        result = validate_player(VALID, SPECS)

        self.assertTrue(result.is_valid)
        self.assertEqual(result.warnings, [])
        self.assertEqual(result.values["debut_year"], "2015")  # ints from the model become text
        self.assertEqual(result.name, "Test Player")

    def test_missing_or_blank_values_are_errors(self):
        p = player(combined_runs="  ")
        del p["role"]

        result = validate_player(p, SPECS)

        self.assertFalse(result.is_valid)
        self.assertIn("role is missing", result.errors)
        self.assertIn("combined_runs is missing", result.errors)

    def test_missing_name_is_an_error(self):
        self.assertIn("name is missing", validate_player(player(name=None), SPECS).errors)

    def test_numbers_must_be_whole_numbers(self):
        for bad in ["~300", "1,234", "12.5", "-3", "about 40", True]:
            with self.subTest(value=bad):
                result = validate_player(player(combined_runs=bad), SPECS)
                self.assertFalse(result.is_valid)

    def test_numbers_are_normalized(self):
        self.assertEqual(validate_player(player(combined_runs=" 0042 "), SPECS).values["combined_runs"], "42")

    def test_fixed_vocabulary_accepts_any_case_and_uses_the_game_spelling(self):
        result = validate_player(player(bowling_style="right-ARM pace"), SPECS)

        self.assertTrue(result.is_valid)
        self.assertEqual(result.values["bowling_style"], "Right-arm Pace")

    def test_fixed_vocabulary_rejects_unknown_values_with_a_suggestion(self):
        result = validate_player(player(role="Wicketkeeper Batter"), SPECS)

        self.assertFalse(result.is_valid)
        self.assertIn("did you mean 'Wicketkeeper-Batter'?", result.errors[0])
        self.assertIn("accepted: Batter, Bowler, Wicketkeeper-Batter", result.errors[0])

    def test_new_country_is_a_warning_not_an_error(self):
        result = validate_player(player(country="UAE"), SPECS)

        self.assertTrue(result.is_valid)
        self.assertEqual(result.values["country"], "UAE")
        self.assertIn("country 'UAE' is new to the game", result.warnings[0])

    def test_new_country_close_to_an_existing_one_suggests_it(self):
        result = validate_player(player(country="New Zeeland"), SPECS)

        self.assertIn("did you mean 'New Zealand'?", result.warnings[0])

    def test_country_alias_is_written_in_the_games_spelling(self):
        result = validate_player(player(country="Great Britain"), SPECS)

        self.assertTrue(result.is_valid)
        self.assertEqual(result.values["country"], "United Kingdom")
        self.assertEqual(result.warnings, [])

    def test_country_alias_ignores_case_and_spaces(self):
        specs = [AttributeSpec("country", CATEGORICAL, frozenset({"USA"}))]

        result = validate_player({"name": "Test Player", "country": " united states "}, specs)

        self.assertEqual(result.values["country"], "USA")
        self.assertEqual(result.warnings, [])

    def test_country_alias_wins_over_a_stored_alias_spelling(self):
        # A sport that already has both spellings still gets the standard one.
        specs = [AttributeSpec("country", CATEGORICAL, frozenset({"USA", "United States"}))]

        result = validate_player({"name": "Test Player", "country": "United States"}, specs)

        self.assertEqual(result.values["country"], "USA")

    def test_aliases_only_apply_to_country(self):
        specs = [AttributeSpec("team", CATEGORICAL, frozenset())]

        result = validate_player({"name": "Test Player", "team": "UK"}, specs)

        self.assertEqual(result.values["team"], "UK")

    def test_attribute_with_no_existing_values_accepts_with_a_warning(self):
        specs = [AttributeSpec("position", CATEGORICAL, frozenset())]

        result = validate_player({"name": "New Sport Player", "position": "Goalkeeper"}, specs)

        self.assertTrue(result.is_valid)
        self.assertEqual(len(result.warnings), 1)


class TierTests(unittest.TestCase):
    def test_parse_tiers_accepts_any_combination_and_case(self):
        self.assertEqual(parse_tiers("Medium, HARD"), frozenset({"medium", "hard"}))
        self.assertEqual(parse_tiers("easy"), frozenset({"easy"}))

    def test_parse_tiers_any_means_no_preference(self):
        self.assertIsNone(parse_tiers("any"))
        self.assertIsNone(parse_tiers(" ANY "))

    def test_parse_tiers_rejects_unknown_or_empty(self):
        for bad in ["expert", "medium,impossible", "", " , ", "any,hard"]:
            with self.subTest(value=bad):
                with self.assertRaises(ValueError):
                    parse_tiers(bad)

    def test_describe_tiers_is_always_easy_to_hard(self):
        self.assertEqual(describe_tiers(frozenset({"hard", "easy"})), "easy or hard")

    def test_tier_warning(self):
        wanted = frozenset({"medium", "hard"})

        self.assertEqual(tier_warning("easy", wanted), "tier is easy, but you asked for medium or hard")
        self.assertIsNone(tier_warning("hard", wanted))
        self.assertIsNone(tier_warning("easy", None))  # no preference
        self.assertIsNone(tier_warning("?", wanted))  # tier unknown (API not available)
        self.assertIsNone(tier_warning("-", wanted))  # player has errors


class SchemaMismatchTests(unittest.TestCase):
    def test_matching_fields_have_no_problems(self):
        fields = ["name", "country", "role", "bowling_style", "combined_runs", "debut_year"]
        self.assertEqual(schema_mismatch(fields, SPECS), [])

    def test_reports_both_directions(self):
        problems = schema_mismatch(["name", "country", "role", "bowling_style", "combined_runs", "batting_hand"], SPECS)

        self.assertIn("the database has no 'batting_hand' column for this sport", problems)
        self.assertIn("the agent doesn't research the 'debut_year' column", problems)


class PromptCountriesTests(unittest.TestCase):
    def test_sorted_and_without_alias_spellings(self):
        specs = [AttributeSpec("country", CATEGORICAL, frozenset({"USA", "United States", "Spain", "Great Britain"}))]

        self.assertEqual(prompt_countries(specs), ["Spain", "USA"])

    def test_empty_without_a_country_column(self):
        self.assertEqual(prompt_countries([AttributeSpec("role", CATEGORICAL, frozenset({"Batter"}))]), [])


class WriteConfirmationTests(unittest.TestCase):
    def test_this_pc_needs_yes(self):
        for host in (None, "", "localhost", "LOCALHOST", "127.0.0.1", "::1"):
            self.assertEqual(write_confirmation(host, "idtheathlete"), "yes", host)

    def test_another_server_needs_the_database_name(self):
        self.assertEqual(write_confirmation("dpg-abc.singapore-postgres.render.com", "prod_db"), "prod_db")
        self.assertEqual(write_confirmation("host.docker.internal", "prod_db"), "prod_db")


if __name__ == "__main__":
    unittest.main()
