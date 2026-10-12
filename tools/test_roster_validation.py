"""Run from the tools folder: python -m unittest test_roster_validation"""

import unittest

from roster_validation import (
    CATEGORICAL,
    NUMERIC,
    AttributeSpec,
    schema_mismatch,
    validate_player,
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

    def test_attribute_with_no_existing_values_accepts_with_a_warning(self):
        specs = [AttributeSpec("position", CATEGORICAL, frozenset())]

        result = validate_player({"name": "New Sport Player", "position": "Goalkeeper"}, specs)

        self.assertTrue(result.is_valid)
        self.assertEqual(len(result.warnings), 1)


class SchemaMismatchTests(unittest.TestCase):
    def test_matching_fields_have_no_problems(self):
        fields = ["name", "country", "role", "bowling_style", "combined_runs", "debut_year"]
        self.assertEqual(schema_mismatch(fields, SPECS), [])

    def test_reports_both_directions(self):
        problems = schema_mismatch(["name", "country", "role", "bowling_style", "combined_runs", "batting_hand"], SPECS)

        self.assertIn("the database has no 'batting_hand' column for this sport", problems)
        self.assertIn("the agent doesn't research the 'debut_year' column", problems)


if __name__ == "__main__":
    unittest.main()
