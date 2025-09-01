import requests
import os
import re
import tmdbsimple as tmdb

from config import TMDB_API_KEY

tmdb.REQUESTS_TIMEOUT = 15

class TMDB:
    def __init__(self, api_key: str = TMDB_API_KEY):
        self.api_key = api_key
        tmdb.API_KEY = self.api_key
        session = requests.Session()
        tmdb.REQUESTS_SESSION = session

    @staticmethod
    def clean_filename(filename: str) -> dict:
        
        # Detect type (movie or tv show)
        def detect_hint_type(n: str) -> str:
            if (re.search(r"[Ss]\d{1,2}[Ee]\d{1,3}", n) or 
                re.search(r"\d{1,2}x\d{1,3}", n) or
                re.search(r"\bTemporada\s+\d+\b", n, re.IGNORECASE) or
                re.search(r"\bSeason\s+\d+\b", n, re.IGNORECASE)):
                return "tv"
            return "movie"
        
        # Remove main extension
        name = os.path.splitext(filename)[0]

        # Check for TMDB id
        tmdbid = None
        match = re.search(r"\[tmdbid-(\d+)\]", name, re.IGNORECASE)
        if match:
            tmdbid = int(match.group(1))
            name = re.sub(r"\[tmdbid-\d+\]", "", name)

        # Remove common noise patterns (resolution, quality, part numbers, etc.)
        noise_patterns = [
            r"\b\d{3,4}p\b",         # 1080p, 720p
            r"\bBlu[- ]?ray\b",
            r"\bHEVC\b",
            r"\bWEB[- ]?DL\b",
            r"\bHDRip\b",
            r"\bDVDRip\b",
            r"\.part\d+",            # .part1, .part2
            r"\.\d+$",               # .001, .002
            r"\[.*?\]",              # [anything]
            r"\(.*?\)",              # (anything)
            r"\bAC3\b",              # 🔥 remove AC3
            r"\bDTS\b",
            r"\bXviD\b",
            r"\bx264\b",
            r"\bH\.?264\b",
            r"\bAAC\b",
            r"\bMP3\b",
        ]

        for pattern in noise_patterns:
            name = re.sub(pattern, "", name, flags=re.IGNORECASE)

        # Remove extra stacked extensions (e.g. .mkv.zip.001)
        name = re.sub(r"\.(zip|7z|rar|mkv|avi|mp4)$", "", name, flags=re.IGNORECASE)

        content_type = detect_hint_type(name)

        # Remove episode markers (e.g. "1x125", "S05E10") from the clean name
        if content_type == "tv":
            # Cut after SxxExx or Nxx
            name = re.split(r"[Ss]\d{1,2}[Ee]\d{1,3}", name)[0]
            name = re.split(r"\d{1,2}x\d{1,3}", name)[0]
            # Also cut after " - "
            name = re.split(r" - ", name)[0]

        name = re.sub(r"\.(mkv|avi|mp4)$", "", name, flags=re.IGNORECASE)

        # Final cleanup: remove extra spaces, dashes, underscores
        name = re.sub(r"\s+", " ", name)
        name = re.sub(r"[-_]+", " ", name)
        name = name.strip()

        return {
            "tmdbid": tmdbid,
            "clean_name": name,
            "type": content_type
        }
    
    def identify_by_tmdbid(self, tmdbid: int, content_type: str) -> dict:
        """Identify a movie or series by its TMDB ID."""
        
        if not tmdbid:
            raise ValueError("No TMDB ID provided.")
        
        # TMDB does not provide unique IDs for movies and series, so we need to check both types.
        
        try_movie = None
        try_series = None
        
        if content_type == "movie":
            try_movie = self.get_movie(tmdbid)
            if try_movie:
                return try_movie
        elif content_type == "tv":
            try_series = self.get_tv(tmdbid)
            if try_series:
                return try_series
        
        if not content_type:
            # If no type is specified, try both
            try_movie = self.get_movie(tmdbid)
            try_series = self.get_tv(tmdbid)
        
        if try_movie:
            return try_movie
        elif try_series:
            return try_series
        
        return {}

    def get_tv(self, tmdbid):
        try_series = None
        try:
            try_series = tmdb.TV(tmdbid).info(language='es-ES')
            try_series["media_type"] = "tv"
        except Exception as e:
            print(f"Invalid TMDB ID for series: {tmdbid}. Error: {e}")
        return try_series

    def get_movie(self, tmdbid):
        try_movie = None
        try:
            try_movie = tmdb.Movies(tmdbid).info(language='es-ES')
            try_movie["media_type"] = "movie"
        except Exception as e:
            print(f"Invalid TMDB ID for movie: {tmdbid}. Error: {e}")
        return try_movie
            
    def identify_by_filename(self, filename: str) -> dict:
        """Identify a movie or series by its filename."""
        file = self.clean_filename(filename)
        response = None
        if file["tmdbid"]:
            response = self.identify_by_tmdbid(file["tmdbid"], file["type"])
        
        if not response:    
            search = tmdb.Search()
            
            if file["type"] == "movie":
                search.movie(query=file["clean_name"], language='es-ES')
                if search.results:
                    search.results[0]["media_type"] = "movie"
            elif file["type"] == "tv":
                search.tv(query=file["clean_name"], language='es-ES')
                if search.results:
                    search.results[0]["media_type"] = "tv"
            
            if not search.results:
                search.multi(query=file["clean_name"], language='es-ES')
            
            response = search.results[0] if search.results else {}
            
        return response
            
