import { ref, computed } from 'vue'
import { apiUrl } from '../config'
import { findJellyfinItemByTmdbId } from '../api/jellyfin'
import type { CatalogMovie, CatalogSeries, SeriesMatchPlan } from '../api/cinegram'

/** Loads a catalogue movie or series and runs the actions that act on the whole title. */
export function useCatalogItem(props: { type: string; id: string }) {
  const movie = ref<CatalogMovie | null>(null)
  const series = ref<CatalogSeries | null>(null)
  const item = computed(() => movie.value ?? series.value)
  const isLoading = ref(true)
  const jellyfinItemId = ref<string | null>(null)
  const matchingWithJellyfin = ref(false)

  const itemPath = computed(() => props.type === 'movies' ? `/movies/${props.id}` : `/series/${props.id}`)

  const checkJellyfinItem = async (tmdbId: number) => {
    try {
      const jfId = await findJellyfinItemByTmdbId(tmdbId, props.type === 'movies' ? 'movie' : 'series')
      if (jfId) {
        jellyfinItemId.value = jfId
      }
    } catch (err) {
      console.error("Error finding Jellyfin item:", err)
    }
  }

  const fetchItem = async () => {
    isLoading.value = true
    try {
      const res = await fetch(`${apiUrl}${itemPath.value}`)
      if (res.ok) {
        const data: CatalogMovie | CatalogSeries = await res.json()
        if ('seasons' in data && data.seasons) {
          data.seasons.sort((a, b) => a.season_number - b.season_number)
          data.seasons.forEach((season) => {
            if (season.episodes) {
              season.episodes.sort((a, b) => a.episode_number - b.episode_number)
            }
          })
        }
        movie.value = props.type === 'movies' ? data as CatalogMovie : null
        series.value = props.type === 'movies' ? null : data as CatalogSeries
        if (data.tmdb_id) {
          checkJellyfinItem(data.tmdb_id)
        }
      }
    } catch (err) {
      console.error(err)
    } finally {
      isLoading.value = false
    }
  }

  const reidentify = async (newTmdbId: number) => {
    isLoading.value = true
    try {
      const res = await fetch(`${apiUrl}${itemPath.value}/reidentify?new_tmdb_id=${newTmdbId}`, { method: 'POST' })
      if (res.ok) {
        alert("Item re-identified successfully. Info has been updated and files re-mapped.")
        fetchItem()
      } else {
        const errText = await res.text()
        alert("Error re-identifying: " + errText)
      }
    } catch (err) {
      console.error(err)
      alert("Connection error.")
    } finally {
      isLoading.value = false
    }
  }

  const setPoster = async (path: string): Promise<boolean> => {
    try {
      const res = await fetch(`${apiUrl}${itemPath.value}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ poster_path: path })
      })
      if (res.ok) {
        alert("Poster updated successfully.")
        fetchItem()
        return true
      }
      alert("Error updating the poster.")
    } catch (err) {
      console.error(err)
      alert("Connection error.")
    }
    return false
  }

  const matchWithJellyfin = async () => {
    matchingWithJellyfin.value = true
    try {
      const res = await fetch(`${apiUrl}/probe/series/${props.id}`, { method: 'POST' })
      if (res.ok) {
        const plan: SeriesMatchPlan = await res.json()
        const matched = plan.matches?.length ?? 0
        const skipped = plan.skips ?? []
        let message = `✅ Matched ${matched} episode${matched === 1 ? '' : 's'} with Jellyfin.`
        if (skipped.length > 0) {
          message += `\n\nSkipped ${skipped.length}:\n` + skipped
            .map((s) => `S${String(s.season).padStart(2, '0')}E${String(s.episode).padStart(2, '0')}: ${s.reason}`)
            .join('\n')
        }
        alert(message)
        fetchItem()
      } else {
        const err = await res.text()
        alert('❌ Error matching with Jellyfin: ' + err)
      }
    } catch (err) {
      console.error(err)
      alert('❌ Connection error.')
    } finally {
      matchingWithJellyfin.value = false
    }
  }

  const deleteItem = async () => {
    const confirmMsg = props.type === 'movies'
      ? "Are you sure you want to delete this movie? All collections will be unlinked."
      : "Are you sure you want to delete this series? All seasons and episodes will be unlinked."
    if (!confirm(confirmMsg)) return

    try {
      const res = await fetch(`${apiUrl}${itemPath.value}`, { method: 'DELETE' })
      if (res.ok) {
        alert("Deleted successfully.")
        window.history.back()
      } else {
        alert("Error deleting.")
      }
    } catch (err) {
      console.error(err)
    }
  }

  return {
    item,
    movie,
    series,
    isLoading,
    itemPath,
    jellyfinItemId,
    matchingWithJellyfin,
    fetchItem,
    reidentify,
    setPoster,
    matchWithJellyfin,
    deleteItem
  }
}
