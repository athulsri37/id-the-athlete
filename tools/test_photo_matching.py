"""Tests for the photo review's matching (photo_matching.py).
Run: python -m unittest test_photo_matching"""

import unittest

from photo_matching import (
    AMBIGUOUS,
    CRICKETER,
    FEMALE,
    MALE,
    MAX_CREDIT_LENGTH,
    NO_PHOTO,
    NOT_FOUND,
    PHOTO,
    PHOTO_PROFILES,
    TENNIS_PLAYER,
    Person,
    commons_page_url,
    commons_thumb_url,
    credits_from_imageinfo,
    match_player,
    normalize_name,
    person_from_entity,
    plain_text,
)

COUNTRIES = {"Q39": "switzerland", "Q408": "australia", "Q16": "canada", "Q21": "england", "Q145": "united kingdom"}
TENNIS_MEN = PHOTO_PROFILES["tennis-men"]
CRICKET_MEN = PHOTO_PROFILES["cricket-men-international"]


def person(qid="Q1", name="roger federer", occupation=TENNIS_PLAYER, sex=MALE, countries=("Q39",), born=1981,
           image="Federer.jpg") -> Person:
    return Person(qid, frozenset({name}), frozenset({occupation}), frozenset({sex} if sex else set()),
                  frozenset(countries), born, image)


class EveryTourHasAProfile(unittest.TestCase):
    def test_the_four_tours(self):
        self.assertEqual(
            set(PHOTO_PROFILES),
            {"tennis-men", "tennis-women", "cricket-men-international", "cricket-women-international"},
        )


class Names(unittest.TestCase):
    def test_accents_dots_hyphens_and_disambiguation_are_ignored(self):
        self.assertEqual(normalize_name("Leylah Fernández"), "leylah fernandez")
        self.assertEqual(normalize_name("M.S. Dhoni"), "m s dhoni")
        self.assertEqual(normalize_name("Nat Sciver-Brunt"), "nat sciver brunt")
        self.assertEqual(normalize_name("John Smith (New Zealand)"), "john smith")
        self.assertEqual(normalize_name("John Smith (b. 1985)"), "john smith")


class Matching(unittest.TestCase):
    def test_the_one_fitting_person_with_a_photo(self):
        match = match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, [person()], COUNTRIES)
        self.assertEqual((match.status, match.person.qid, match.flags), (PHOTO, "Q1", ()))

    def test_a_fitting_person_without_a_photo(self):
        match = match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, [person(image=None)], COUNTRIES)
        self.assertEqual(match.status, NO_PHOTO)

    def test_a_namesake_from_another_era_is_ruled_out(self):
        # The modern George Bailey, not the one photographed in 1878.
        old = person("Q2", "george bailey", CRICKETER, countries=("Q408",), born=1854, image="GH Bailey in 1878.jpg")
        new = person("Q3", "george bailey", CRICKETER, countries=("Q408",), born=1982, image="George Bailey 2008.jpg")
        match = match_player("George Bailey", "Australia", 2005, CRICKET_MEN, [old, new], COUNTRIES)
        self.assertEqual((match.status, match.person.qid), (PHOTO, "Q3"))

    def test_another_sport_or_sex_or_name_is_not_a_match(self):
        people = [
            person("Q4", occupation=CRICKETER),
            person("Q5", sex=FEMALE),
            person("Q6", name="roger federer junior"),
        ]
        self.assertEqual(match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, people, COUNTRIES).status,
                         NOT_FOUND)

    def test_a_person_without_a_listed_sex_or_birth_can_still_match(self):
        match = match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, [person(sex=None, born=None)], COUNTRIES)
        self.assertEqual(match.status, PHOTO)

    def test_the_same_country_breaks_a_tie(self):
        swiss = person("Q7")
        other = person("Q8", countries=("Q408",))
        match = match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, [other, swiss], COUNTRIES)
        self.assertEqual(match.person.qid, "Q7")

    def test_two_people_who_both_fit_are_ambiguous(self):
        match = match_player("Roger Federer", "Switzerland", 1998, TENNIS_MEN, [person("Q7"), person("Q9")], COUNTRIES)
        self.assertEqual(match.status, AMBIGUOUS)

    def test_the_only_fit_with_another_country_is_flagged_not_dropped(self):
        listed_elsewhere = person("Q10", "jos buttler", CRICKETER, countries=("Q408",), born=1990, image="Buttler.jpg")
        match = match_player("Jos Buttler", "England", 2011, CRICKET_MEN, [listed_elsewhere], COUNTRIES)
        self.assertEqual(match.status, PHOTO)
        self.assertEqual(match.flags, ("Wikidata's country is Australia, not England",))

    def test_country_names_wikidata_writes_differently_still_match(self):
        brit = person("Q11", "jos buttler", CRICKETER, countries=("Q145",), born=1990)
        match = match_player("Jos Buttler", "England", 2011, CRICKET_MEN, [brit], COUNTRIES)
        self.assertEqual(match.flags, ())


class Entities(unittest.TestCase):
    def test_a_wikidata_item_with_its_name_only_in_mul(self):
        entity = {
            "id": "Q1426",
            "labels": {"mul": {"value": "Roger Federer"}},
            "aliases": {"en": [{"value": "Federer"}]},
            "claims": {
                "P106": [{"mainsnak": {"datavalue": {"value": {"id": TENNIS_PLAYER}}}}],
                "P21": [{"mainsnak": {"datavalue": {"value": {"id": MALE}}}}],
                "P569": [{"mainsnak": {"datavalue": {"value": {"time": "+1981-08-08T00:00:00Z"}}}}],
                "P27": [{"mainsnak": {"datavalue": {"value": {"id": "Q39"}}}}],
                "P18": [{"mainsnak": {"datavalue": {"value": "Federer WM16.jpg"}}}],
                "P1532": [{"mainsnak": {"snaktype": "novalue"}}],
            },
        }
        found = person_from_entity(entity)
        self.assertEqual(found.names, {"roger federer", "federer"})
        self.assertEqual((found.birth_year, found.image, found.country_ids), (1981, "Federer WM16.jpg", {"Q39"}))


class Credits(unittest.TestCase):
    def test_credit_and_licence_per_file_skipping_unusable_ones(self):
        response = {
            "query": {
                "pages": {
                    "1": {"title": "File:A.jpg", "imageinfo": [{"extmetadata": {
                        "Artist": {"value": '<a href="https://flickr.com/x">si.robi</a>'},
                        "LicenseShortName": {"value": "CC BY-SA 2.0"}}}]},
                    "2": {"title": "File:No author.jpg", "imageinfo": [{"extmetadata": {
                        "LicenseShortName": {"value": "CC BY 4.0"}}}]},
                    "-1": {"title": "File:Gone.jpg", "missing": ""},
                }
            }
        }
        found = credits_from_imageinfo(response)
        self.assertEqual(list(found), ["A.jpg"])
        self.assertEqual((found["A.jpg"].credit, found["A.jpg"].license), ("si.robi", "CC BY-SA 2.0"))

    def test_a_long_credit_is_shortened_to_what_the_server_accepts(self):
        text = plain_text("<p>" + "word " * 200 + "</p>", MAX_CREDIT_LENGTH)
        self.assertEqual(len(text), MAX_CREDIT_LENGTH)
        self.assertTrue(text.endswith("…"))

    def test_commons_addresses(self):
        self.assertEqual(commons_thumb_url("Federer WM16 (37).jpg", 250),
                         "https://commons.wikimedia.org/wiki/Special:FilePath/Federer_WM16_%2837%29.jpg?width=250")
        self.assertEqual(commons_page_url("A b.jpg"), "https://commons.wikimedia.org/wiki/File:A_b.jpg")


if __name__ == "__main__":
    unittest.main()
