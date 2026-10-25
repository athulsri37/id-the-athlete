"""Run from the tools folder: python -m unittest test_stat_refresh"""

import unittest
from datetime import date

from stat_refresh import (
    check_refresh,
    held_players,
    match_answers,
    plan_apply,
    refreshable_keys,
    review_lines,
    same_name,
    search_name,
    shown_checks,
    tier_cell,
)

TENNIS = {
    "country": "Italy",
    "plays": "Right",
    "turned_pro_year": "2018",
    "grand_slam_titles": "4",
    "career_titles": "18",
    "career_high_ranking": "1",
    "active_status": "Active",
}

CRICKET = {
    "country": "India",
    "debut_year": "2008",
    "combined_matches": "500",
    "combined_runs": "26000",
    "combined_wickets": "4",
    "active_status": "Active",
}


def tennis(**proposed):
    return check_refresh(1, "Jannik Sinner", TENNIS, {**_same(TENNIS), **proposed})


def cricket(**proposed):
    return check_refresh(2, "Virat Kohli", CRICKET, {**_same(CRICKET), **proposed})


def _same(current):
    return {key: current[key] for key in refreshable_keys(current)}


class RefreshableKeysTests(unittest.TestCase):
    def test_only_changeable_columns_in_a_stable_order(self):
        self.assertEqual(
            refreshable_keys(TENNIS), ["grand_slam_titles", "career_titles", "career_high_ranking", "active_status"]
        )
        self.assertEqual(
            refreshable_keys(CRICKET), ["combined_matches", "combined_runs", "combined_wickets", "active_status"]
        )


class CheckRefreshTests(unittest.TestCase):
    def test_unchanged_answer_has_no_changes_or_issues(self):
        result = tennis()

        self.assertEqual(result.changes, {})
        self.assertEqual((result.errors, result.warnings, result.unsure), ([], [], []))

    def test_rise_is_a_change_in_clean_form(self):
        result = tennis(career_titles=" 019 ")

        self.assertTrue(result.is_valid)
        self.assertEqual(result.changes, {"career_titles": ("18", "19")})
        self.assertEqual(result.warnings, [])

    def test_fixed_columns_are_never_changed(self):
        result = check_refresh(1, "Jannik Sinner", TENNIS, {**_same(TENNIS), "country": "Spain", "turned_pro_year": 2019})

        self.assertEqual(result.changes, {})

    def test_unsure_or_missing_keeps_the_stored_value(self):
        result = check_refresh(1, "Jannik Sinner", TENNIS, {"career_titles": "Unsure", "grand_slam_titles": None})

        self.assertEqual(result.changes, {})
        self.assertEqual(result.unsure, ["grand_slam_titles", "career_titles", "career_high_ranking", "active_status"])
        self.assertTrue(result.is_valid)

    def test_total_going_down_is_an_error(self):
        result = tennis(career_titles="17")

        self.assertFalse(result.is_valid)
        self.assertIn("career_titles went down, 18 -> 17", result.errors[0])

    def test_one_error_blocks_the_whole_player(self):
        result = tennis(career_titles="17", grand_slam_titles="5")

        self.assertFalse(result.is_valid)
        self.assertIn("grand_slam_titles", result.changes)  # shown, but not written

    def test_career_high_getting_worse_is_an_error(self):
        result = check_refresh(3, "Player", {**TENNIS, "career_high_ranking": "8"}, {**_same(TENNIS), "career_high_ranking": "15"})

        self.assertFalse(result.is_valid)
        self.assertIn("career_high_ranking got worse, 8 -> 15", result.errors[0])

    def test_career_high_improving_is_fine_and_reaching_no_1_is_checked(self):
        current = {**TENNIS, "career_high_ranking": "8"}

        better = check_refresh(3, "Player", current, {**_same(current), "career_high_ranking": "5"})
        top = check_refresh(3, "Player", current, {**_same(current), "career_high_ranking": "1"})

        self.assertEqual((better.errors, better.warnings), ([], []))
        self.assertTrue(top.is_valid)
        self.assertIn("check the player reached No. 1", top.warnings[0])

    def test_big_jump_is_a_warning_not_an_error(self):
        result = tennis(career_titles="22")

        self.assertTrue(result.is_valid)
        self.assertIn("career_titles rose by 4", result.warnings[0])

    def test_jump_at_the_limit_is_not_flagged(self):
        self.assertEqual(tennis(career_titles="21").warnings, [])

    def test_retiring_is_a_warning(self):
        result = tennis(active_status="retired")

        self.assertTrue(result.is_valid)
        self.assertEqual(result.changes["active_status"], ("Active", "Retired"))
        self.assertIn("check the player has retired", result.warnings[0])

    def test_coming_back_from_retirement_is_an_error(self):
        current = {**TENNIS, "active_status": "Retired"}

        result = check_refresh(1, "Player", current, {**_same(current), "active_status": "Active"})

        self.assertFalse(result.is_valid)
        self.assertIn("set it in the Control Room", result.errors[0])

    def test_status_must_be_active_or_retired(self):
        result = tennis(active_status="Injured")

        self.assertFalse(result.is_valid)
        self.assertIn("must be Active or Retired", result.errors[0])

    def test_non_numbers_are_errors(self):
        for value in ("19.5", "-1", "about 20", True):
            with self.subTest(value=value):
                self.assertFalse(tennis(career_titles=value).is_valid)

    def test_bad_stored_value_is_an_error(self):
        current = {**TENNIS, "career_titles": "n/a"}

        result = check_refresh(1, "Player", current, {**_same(current), "career_titles": "19"})

        self.assertFalse(result.is_valid)
        self.assertIn("stored value 'n/a'", result.errors[0])

    def test_runs_rising_without_matches_is_an_error(self):
        result = cricket(combined_runs="26500")

        self.assertFalse(result.is_valid)
        self.assertIn("combined_runs rose but combined_matches didn't", result.errors[0])

    def test_runs_and_wickets_rising_with_matches_is_fine(self):
        result = cricket(combined_matches="505", combined_runs="26300", combined_wickets="5")

        self.assertEqual((result.errors, result.warnings), ([], []))
        self.assertEqual(len(result.changes), 3)

    def test_matches_unsure_and_runs_rising_is_an_error(self):
        result = cricket(combined_matches="unsure", combined_runs="26300")

        self.assertFalse(result.is_valid)

    def test_more_grand_slams_than_titles_is_an_error(self):
        current = {**TENNIS, "grand_slam_titles": "2", "career_titles": "2"}

        result = check_refresh(1, "Player", current, {**_same(current), "grand_slam_titles": "3"})

        self.assertFalse(result.is_valid)
        self.assertIn("grand_slam_titles (3) is more than career_titles (2)", result.errors[0])


    def test_older_stored_slams_above_titles_doesnt_block_a_retirement(self):
        current = {**TENNIS, "grand_slam_titles": "3", "career_titles": "2"}

        result = check_refresh(1, "Player", current, {**_same(current), "active_status": "Retired"})

        self.assertTrue(result.is_valid)


class NameTests(unittest.TestCase):
    def test_search_name_drops_the_disambiguation(self):
        self.assertEqual(search_name("John Smith (New Zealand)"), "John Smith")
        self.assertEqual(search_name("John Smith (b. 1985)"), "John Smith")
        self.assertEqual(search_name("Jannik Sinner"), "Jannik Sinner")

    def test_same_name_ignores_case_accents_punctuation_and_disambiguation(self):
        self.assertTrue(same_name("Albert Montañés", "albert montanes"))
        self.assertTrue(same_name("John Smith (New Zealand)", "John Smith"))
        self.assertTrue(same_name("Jean-Julien Rojer", "Jean Julien Rojer"))
        self.assertFalse(same_name("Stan Wawrinka", "Stanislas Wawrinka"))


class MatchAnswersTests(unittest.TestCase):
    def test_matches_by_id_and_reports_every_unusable_answer(self):
        answers = [
            {"id": 1, "name": "A"},
            {"id": "2", "name": "B"},
            {"id": 2, "name": "B again"},
            {"id": 9, "name": "Stranger"},
            {"id": "x", "name": "No id"},
        ]

        matched, problems = match_answers([1, 2, 3], answers)

        self.assertEqual({k: v["name"] for k, v in matched.items()}, {1: "A", 2: "B"})
        self.assertEqual(
            problems,
            [
                "id 2 was answered twice; the first answer is kept",
                "an answer is for id 9, which wasn't asked for; ignored",
                "an answer has no usable id ('x'); ignored",
                "no answer for id 3",
            ],
        )


class HeldPlayerTests(unittest.TestCase):
    def test_holds_todays_and_yesterdays_answers_until_two_days_after_their_puzzle(self):
        puzzles = [
            (date(2026, 10, 25), 10),
            (date(2026, 10, 24), 11),
            (date(2026, 10, 23), 12),
        ]

        self.assertEqual(
            held_players(puzzles, date(2026, 10, 25)),
            {10: date(2026, 10, 27), 11: date(2026, 10, 26)},
        )

    def test_todays_answer_is_still_held_tomorrow(self):
        puzzles = [(date(2026, 10, 25), 10)]

        self.assertIn(10, held_players(puzzles, date(2026, 10, 26)))
        self.assertNotIn(10, held_players(puzzles, date(2026, 10, 27)))

    def test_answer_of_both_days_is_held_until_the_later_release(self):
        puzzles = [(date(2026, 10, 24), 10), (date(2026, 10, 25), 10)]

        self.assertEqual(held_players(puzzles, date(2026, 10, 25)), {10: date(2026, 10, 27)})

    def test_nothing_held_without_puzzles(self):
        self.assertEqual(held_players([], date(2026, 10, 25)), {})


class PlanApplyTests(unittest.TestCase):
    def test_writes_when_the_database_still_has_the_researched_value(self):
        plan = plan_apply({"career_titles": ("18", "19")}, {"career_titles": "18"})

        self.assertEqual(plan.write, {"career_titles": "19"})
        self.assertTrue(plan.can_write)

    def test_second_apply_writes_nothing(self):
        plan = plan_apply({"career_titles": ("18", "19")}, {"career_titles": "19"})

        self.assertEqual(plan.already, ["career_titles"])
        self.assertFalse(plan.can_write)

    def test_edit_since_research_skips_the_whole_player(self):
        plan = plan_apply(
            {"combined_matches": ("500", "505"), "combined_runs": ("26000", "26300")},
            {"combined_matches": "500", "combined_runs": "26100"},
        )

        self.assertEqual(plan.conflicts, {"combined_runs": ("26000", "26100")})
        self.assertFalse(plan.can_write)


class TierCellTests(unittest.TestCase):
    def test_tier_cell(self):
        self.assertEqual(tier_cell("Hard", "Medium", overridden=False), "Hard -> Medium")
        self.assertEqual(tier_cell("Hard", "Hard", overridden=False), "Hard")
        self.assertEqual(tier_cell("Hard", "Medium", overridden=True), "overridden")
        self.assertEqual(tier_cell(None, "Medium", overridden=False), "?")


class ReviewLinesTests(unittest.TestCase):
    def test_lists_changes_and_errors_only_with_notes(self):
        changed = tennis(career_titles="19")
        retired = check_refresh(4, "Fabio Fognini", TENNIS, {**_same(TENNIS), "active_status": "Retired"})
        broken = tennis(career_titles="17")
        same = cricket()

        lines = review_lines([changed, retired, broken, same], {1: "Easy", 4: "Medium"}, {})
        text = "\n".join(lines)

        self.assertIn("Jannik Sinner | career_titles 18 -> 19", text)
        self.assertIn("ok", lines[2])
        self.assertIn("check", lines[3])
        self.assertIn("ERROR", lines[4])
        self.assertNotIn("Virat Kohli", text)
        self.assertIn("check: active_status Active -> Retired", text)
        self.assertIn("error: career_titles went down", text)
        self.assertEqual(lines[-1], "3 with changes or errors, 1 unchanged, 0 with a value the research wasn't sure of.")

    def test_held_player_is_marked_with_when_to_rerun(self):
        lines = review_lines([tennis(career_titles="19")], {}, {1: date(2026, 10, 27)})

        self.assertIn("held", lines[2])
        self.assertIn("run apply again on or after 27 Oct 2026 (UTC)", "\n".join(lines))

    def test_shown_checks_matches_the_tables_rows(self):
        changed, same, broken = tennis(career_titles="19"), cricket(), tennis(career_titles="17")

        self.assertEqual(shown_checks([changed, same, broken]), [changed, broken])

    def test_nobody_changed_prints_only_the_summary(self):
        self.assertEqual(
            review_lines([tennis(career_titles="unsure")], {}, {}),
            ["0 with changes or errors, 1 unchanged, 1 with a value the research wasn't sure of."],
        )


if __name__ == "__main__":
    unittest.main()
