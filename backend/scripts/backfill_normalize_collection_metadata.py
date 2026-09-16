"""One-off backfill that normalizes existing Collection.quality,
Collection.audio_languages and Collection.subtitle_languages to the fixed
vocabularies introduced alongside Collection.codec (see crud.py:
QUALITY_OPTIONS, CODEC_MAP, LANGUAGE_VOCABULARY), and fills in Collection.codec
from the ffprobe metadata already stored in technical_metadata.

A field is only rewritten when it can be mapped with confidence:
- quality: technical_metadata is re-probed first (authoritative), falling
  back to the same resolution/HDR keyword matching the app already uses
  elsewhere (crud.try_subtract_quality) against the existing free-text value.
- codec: filled in only from technical_metadata; there is no free-text
  fallback since the field never existed before this backfill.
- audio_languages/subtitle_languages: every comma-separated token must match
  a known ISO code or one of the common aliases below; if even one token in
  a value cannot be resolved, the whole value is left untouched rather than
  partially rewritten.

Anything that cannot be confidently mapped is left as-is and printed at the
end for manual review. This script never invents values.

Usage (from the backend/ directory, against the real deployment's database):
    python scripts/backfill_normalize_collection_metadata.py
    python scripts/backfill_normalize_collection_metadata.py --dry-run
"""
import argparse
import sys
import unicodedata
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from sqlmodel import Session, select

import models  # noqa: F401  registers the table schemas
from database import engine
from models import Collection
from crud import (
    DISCARDED_LANGUAGE_CODES, LANGUAGE_CODE_ALIASES, LANGUAGE_VOCABULARY,
    QUALITY_OPTIONS,
    extract_codec_from_metadata, extract_quality_from_metadata, try_subtract_quality,
)

# Free-text tokens accepted on top of the ISO codes themselves: the common
# ISO 639-1 codes, and the English/Spanish names for each language. Not
# exhaustive on purpose: anything not listed here is left for manual review
# instead of being guessed at.
LANGUAGE_ALIASES: dict[str, str] = {
    "es": "spa", "spanish": "spa", "espanol": "spa", "castellano": "spa", "castilian": "spa",
    "en": "eng", "english": "eng", "ingles": "eng",
    "ja": "jpn", "jp": "jpn", "japanese": "jpn", "japones": "jpn",
    "ko": "kor", "korean": "kor", "coreano": "kor",
    "zh": "chi", "chinese": "chi", "chino": "chi", "mandarin": "chi",
    "fr": "fre", "french": "fre", "frances": "fre",
    "de": "ger", "german": "ger", "aleman": "ger",
    "it": "ita", "italian": "ita", "italiano": "ita",
    "pt": "por", "pt-br": "por", "portuguese": "por", "portugues": "por", "brazilian": "por",
    "ru": "rus", "russian": "rus", "ruso": "rus",
    "ar": "ara", "arabic": "ara", "arabe": "ara",
    "ca": "cat", "catalan": "cat",
    "la": "lat", "latin": "lat",
    "pl": "pol", "polish": "pol", "polaco": "pol",
    "tr": "tur", "turkish": "tur", "turco": "tur",
    "hi": "hin", "hindi": "hin",
    "vi": "vie", "vietnamese": "vie", "vietnamita": "vie",
    "th": "tha", "thai": "tha", "tailandes": "tha",
    "id": "ind", "indonesian": "ind", "indonesio": "ind",
    "nl": "dut", "dutch": "dut", "neerlandes": "dut", "holandes": "dut",
    "sv": "swe", "swedish": "swe", "sueco": "swe",
    "no": "nor", "norwegian": "nor", "noruego": "nor",
    "da": "dan", "danish": "dan", "danes": "dan",
    "fi": "fin", "finnish": "fin", "finlandes": "fin",
    "el": "gre", "greek": "gre", "griego": "gre",
    "he": "heb", "hebrew": "heb", "hebreo": "heb",
    "uk": "ukr", "ukrainian": "ukr", "ucraniano": "ukr",
    "hu": "hun", "hungarian": "hun", "hungaro": "hun",
    "cs": "cze", "czech": "cze", "checo": "cze",
    "ro": "rum", "romanian": "rum", "rumano": "rum",
}


def strip_accents(text: str) -> str:
    return "".join(
        c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn"
    )


def resolve_language_token(token: str) -> str | None:
    token = strip_accents(token.strip().lower())
    if not token or token in DISCARDED_LANGUAGE_CODES:
        return None
    resolved = LANGUAGE_CODE_ALIASES.get(token, token)
    if resolved in LANGUAGE_VOCABULARY:
        return resolved
    return LANGUAGE_ALIASES.get(token)


def normalize_language_field(value: str | None) -> tuple[str | None, bool]:
    """Returns (normalized_value, resolved). resolved=True means every token
    in the free-text value confidently mapped to a vocabulary code (safe to
    store even if unchanged from the original). resolved=False means at
    least one token could not be mapped, in which case normalized_value is
    the original value, untouched."""
    if not value or not value.strip():
        return value, True

    tokens = [t.strip() for t in value.split(",") if t.strip()]
    if not tokens:
        return value, True

    resolved_codes: list[str] = []
    for token in tokens:
        code = resolve_language_token(token)
        if code is None:
            return value, False
        if code not in resolved_codes:
            resolved_codes.append(code)

    return ",".join(resolved_codes), True


def normalize_quality_field(collection: Collection) -> tuple[str | None, bool]:
    """Returns (new_value, changed)."""
    current = collection.quality
    if current in QUALITY_OPTIONS:
        return current, False

    if collection.technical_metadata:
        probed = extract_quality_from_metadata(collection.technical_metadata)
        if probed:
            return probed, probed != current

    if not current:
        return current, False

    resolution, hdr = try_subtract_quality(current)
    if not resolution:
        return current, False

    normalized = resolution + (f" {hdr}" if hdr and hdr != "SDR" else "")
    return normalized, normalized != current


def normalize_codec_field(collection: Collection) -> tuple[str | None, bool]:
    """Returns (new_value, changed)."""
    if collection.codec or not collection.technical_metadata:
        return collection.codec, False
    extracted = extract_codec_from_metadata(collection.technical_metadata)
    return (extracted, True) if extracted else (collection.codec, False)


def run(dry_run: bool = False) -> dict:
    stats = {
        "collections_seen": 0,
        "quality_updated": 0,
        "codec_updated": 0,
        "audio_languages_updated": 0,
        "subtitle_languages_updated": 0,
        "needs_review": [],
    }

    with Session(engine) as session:
        collections = session.exec(select(Collection)).all()
        for collection in collections:
            stats["collections_seen"] += 1
            changed = False

            new_quality, quality_changed = normalize_quality_field(collection)
            if quality_changed:
                collection.quality = new_quality
                stats["quality_updated"] += 1
                changed = True
            elif collection.quality and collection.quality not in QUALITY_OPTIONS:
                stats["needs_review"].append((collection.id, "quality", collection.quality))

            new_codec, codec_changed = normalize_codec_field(collection)
            if codec_changed:
                collection.codec = new_codec
                stats["codec_updated"] += 1
                changed = True

            new_audio, audio_resolved = normalize_language_field(collection.audio_languages)
            if audio_resolved:
                if new_audio != collection.audio_languages:
                    collection.audio_languages = new_audio
                    stats["audio_languages_updated"] += 1
                    changed = True
            elif collection.audio_languages:
                stats["needs_review"].append((collection.id, "audio_languages", collection.audio_languages))

            new_subs, subs_resolved = normalize_language_field(collection.subtitle_languages)
            if subs_resolved:
                if new_subs != collection.subtitle_languages:
                    collection.subtitle_languages = new_subs
                    stats["subtitle_languages_updated"] += 1
                    changed = True
            elif collection.subtitle_languages:
                stats["needs_review"].append((collection.id, "subtitle_languages", collection.subtitle_languages))

            if changed and not dry_run:
                session.add(collection)

        if not dry_run:
            session.commit()

    return stats


def main():
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Report what would change without writing to the database"
    )
    args = parser.parse_args()

    stats = run(dry_run=args.dry_run)
    suffix = " (dry run, not saved)" if args.dry_run else ""

    print(f"Collections scanned:        {stats['collections_seen']}")
    print(f"quality updated:            {stats['quality_updated']}{suffix}")
    print(f"codec updated:              {stats['codec_updated']}{suffix}")
    print(f"audio_languages updated:    {stats['audio_languages_updated']}{suffix}")
    print(f"subtitle_languages updated: {stats['subtitle_languages_updated']}{suffix}")

    if stats["needs_review"]:
        print(f"\n{len(stats['needs_review'])} value(s) need manual review:")
        for collection_id, field, value in stats["needs_review"]:
            print(f"  collection {collection_id}: {field} = {value!r}")


if __name__ == "__main__":
    main()
