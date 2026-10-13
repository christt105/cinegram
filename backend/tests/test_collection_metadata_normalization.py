import json

import pytest

import crud


def probe_metadata_with_video(codec_name):
    return json.dumps({
        "streams": [{
            "codec_type": "video",
            "codec_name": codec_name,
            "width": 1920,
            "height": 1080
        }]
    })


# ========================
# CODEC_MAP / extract_codec_from_metadata
# ========================

def test_extract_codec_from_metadata_maps_known_codecs():
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("h264")) == "H.264/AVC"
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("hevc")) == "H.265/HEVC"
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("av1")) == "AV1"


def test_extract_codec_from_metadata_returns_none_for_unknown_codec():
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("mjpeg")) is None


def test_extract_codec_from_metadata_returns_none_without_a_video_stream():
    metadata = json.dumps({"streams": [{"codec_type": "audio", "codec_name": "aac"}]})
    assert crud.extract_codec_from_metadata(metadata) is None


def test_extract_codec_from_metadata_returns_none_for_garbage_input():
    assert crud.extract_codec_from_metadata("not json") is None


# ========================
# Language vocabulary validation
# ========================

def test_validate_language_list_accepts_known_codes():
    assert crud.validate_language_list("spa,eng") == "spa,eng"


def test_validate_language_list_normalizes_case_and_whitespace():
    assert crud.validate_language_list(" SPA , Eng ") == "spa,eng"


def test_validate_language_list_deduplicates():
    assert crud.validate_language_list("spa,spa,eng") == "spa,eng"


def test_validate_language_list_resolves_terminology_aliases():
    # "fra" is the ISO 639-2/T code for French; the vocabulary is keyed by "fre" (639-2/B)
    assert crud.validate_language_list("fra") == "fre"


def test_validate_language_list_discards_und_silently():
    assert crud.validate_language_list("spa,und") == "spa"
    assert crud.validate_language_list("und") is None


def test_validate_language_list_rejects_unknown_codes():
    with pytest.raises(crud.InvalidLanguageCodeError) as exc_info:
        crud.validate_language_list("spa,klingon")
    assert exc_info.value.codes == ["klingon"]


def test_validate_language_list_handles_none_and_empty():
    assert crud.validate_language_list(None) is None
    assert crud.validate_language_list("") is None
    assert crud.validate_language_list("   ") is None
