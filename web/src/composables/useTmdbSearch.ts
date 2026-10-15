import { ref } from 'vue'
import { apiUrl } from '../config'

export interface TmdbSearchResult {
  id: number
  title: string
  year?: number
  media_type: 'movie' | 'tv'
  poster_path?: string
  overview?: string
}

/** TMDB search by name or id, through the server's /tmdb/search. */
export function useTmdbSearch(mediaType: 'movie' | 'tv' | 'multi', initialQuery = '') {
  const query = ref(initialQuery)
  const results = ref<TmdbSearchResult[]>([])
  const searching = ref(false)
  const searched = ref(false)

  const search = async () => {
    if (!query.value.trim()) return
    searching.value = true
    searched.value = true
    results.value = []
    try {
      const res = await fetch(`${apiUrl}/tmdb/search?query=${encodeURIComponent(query.value.trim())}&media_type=${mediaType}`)
      if (res.ok) {
        results.value = await res.json()
      }
    } catch (err) {
      console.error(err)
    } finally {
      searching.value = false
    }
  }

  return { query, results, searching, searched, search }
}
