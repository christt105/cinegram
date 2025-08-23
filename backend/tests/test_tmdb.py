import time
from backend.tmdb import TMDB  # replace with the actual module name where your TMDB class lives

# Initialize with your API key (it’s already set in config)
tmdb_client = TMDB()

# Filenames to test
examples = [
    # "Pokémon 2: El poder de uno (1999).zip.001",
    # "Jurassic World: Dominion [Versión Extendida] (1080p).zip.001",
    # "La guerra del mañana (2021) 1080p.zip.001",
    # "Hayao Miyazaki and the Heron (2024) [tmdbid-1292585].zip.001",
    # "Glass (Cristal) (1080p).zip.001",
    # "Hijos de la anarquía - S05E10 - Crucificado.mkv.zip.001",
    # "Yellowstone 5x08 Un cuchillo y ninguna moneda.mkv",
    # "One Piece 1x125.mkv",
    # "Naruto Shippuden S07E02.avi",
    # "Skinamarink 1080p.part5.rar",
    # "Raised by Wolves 2x08.part3.rar",
    # "Top Gun Maverick IMAX 1080p.part04.rar",
    # "Vikingos - Temporada 3 (Blu-ray 1080p).zip.006",
    # "Pokémon 2: El poder de uno (1999) [tmdbid-12599].zip.001",
    # "Jurassic World: Dominion [Versión Extendida] (1080p) [tmdbid-507086].zip.001",
    # "La guerra del mañana (2021) 1080p [tmdbid-588228].zip.001",
    # "Hayao Miyazaki and the Heron (2024) [tmdbid-1292585].zip.001",
    # "Glass (Cristal) (1080p) [tmdbid-450465].zip.001",
    # "Hijos de la anarquía - S05E10 - Crucificado.mkv [tmdbid-1409].zip.001",
    # "Yellowstone 5x08 Un cuchillo y ninguna moneda [tmdbid-73586].mkv",
    # "One Piece 1x125 [tmdbid-37854].mkv",
    # "Naruto Shippuden - S07E02 - [tmdbid-31910].avi",
    # "Skinamarink 1080p [tmdbid-994143].part5.rar",
    # "Raised by Wolves 2x08 [tmdbid-85723].part3.rar",
    # "Top Gun Maverick IMAX 1080p [tmdbid-361743].part04.rar",
    # "Vikingos - Temporada 3 (Blu-ray 1080p) [tmdbid-44217].zip.006",
    #"Pokémon 2: El poder de uno (1999) [tmdbid-8964512].zip.001",
    "Bailando con los pájaros (2019) 1080p AC3.zip.001",
]

for f in examples:
    # Step 1: clean filename
    cleaned = TMDB.clean_filename(f)
    print("\nFile:", f)
    print("→ Cleaned:", cleaned)

    # Step 2: try to identify on TMDB
    result = tmdb_client.identify_by_filename(f)
    
    print("→ TMDB result:", result.get("title") or result.get("name"), "| ID:", result.get("id"))
    time.sleep(1)

