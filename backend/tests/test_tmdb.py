import pytest

try:
    from backend.tmdb import TMDB
except ImportError:
    from tmdb import TMDB


@pytest.mark.parametrize("raw,expected", [
    ("Pokémon", "pokemon"),
    ("Pokemon", "pokemon"),
    ("WALL·E", "wall e"),
    ("Wall-E", "wall e"),
    ("Wall E", "wall e"),
    ("Spider-Man", "spider man"),
    ("Amélie", "amelie"),
    ("Jurassic World – Dominion", "jurassic world dominion"),
    ("", ""),
])
def test_normalize(raw, expected):
    assert TMDB._normalize(raw) == expected


@pytest.mark.parametrize("query,candidates,expected", [
    # Accented official title, unaccented query (and vice versa)
    ("Pokemon", ["Pokémon", "Digimon"], "Pokémon"),
    ("Pokémon", ["Pokemon", "Monster Rancher"], "Pokemon"),
    # Middle-dot official title against dashed / spaced filenames
    ("Wall-E", ["WALL·E", "Cars"], "WALL·E"),
    ("Wall E", ["WALL·E", "Up"], "WALL·E"),
    # No regression on hyphenated / accented titles that already worked
    ("Spider-Man", ["Spider-Man", "Superman"], "Spider-Man"),
    ("Amelie", ["Amélie", "Delicatessen"], "Amélie"),
])
def test_best_match_normalizes_diacritics(query, candidates, expected):
    results = [{"name": t, "popularity": 1.0} for t in candidates]
    match = TMDB._best_match(results, query, "tv")
    assert match is not None
    assert (match.get("title") or match.get("name")) == expected


def test_best_match_rejects_below_threshold():
    results = [{"name": "Completely Unrelated Show", "popularity": 50.0}]
    assert TMDB._best_match(results, "Pokémon", "tv") is None


def test_best_match_empty_results():
    assert TMDB._best_match([], "Pokémon", "tv") is None
