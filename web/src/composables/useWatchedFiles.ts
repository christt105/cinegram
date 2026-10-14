import { ref, computed, onMounted, onUnmounted } from 'vue'
import { apiUrl } from '../config'
import { listWatchedFiles, reidentifyWatchedFiles, type WatchedFile, type WatchedFileStatus } from '../api/cinegram'

// "confirmed"/"corrected" are transient: the server hasn't picked them up and moved them yet,
// so they still belong with what's pending, not with the finished history below.
const PENDING_STATUSES: WatchedFileStatus[] = ['pending', 'notified', 'confirmed', 'corrected']

/** Files seen in the downloads folder, polled every 5 s while the view is mounted. */
export function useWatchedFiles() {
  const files = ref<WatchedFile[]>([])
  const isLoading = ref(false)
  const isReidentifying = ref(false)
  const isRescanning = ref(false)
  let pollInterval: ReturnType<typeof setInterval> | null = null

  const pendingRows = computed(() => files.value.filter(f => PENDING_STATUSES.includes(f.status)))
  const historyRows = computed(() => files.value.filter(f => !PENDING_STATUSES.includes(f.status)))

  const fetchFiles = async () => {
    isLoading.value = true
    try {
      files.value = await listWatchedFiles()
    } catch (error) {
      console.error('Error fetching watched files:', error)
    } finally {
      isLoading.value = false
    }
  }

  const reidentifyAll = async () => {
    isReidentifying.value = true
    try {
      await reidentifyWatchedFiles()
      await fetchFiles()
    } catch (error) {
      console.error('Error re-identifying watched files:', error)
      alert('Failed to re-identify watched files.')
    } finally {
      isReidentifying.value = false
    }
  }

  const rescanFolder = async () => {
    isRescanning.value = true
    try {
      const res = await fetch(`${apiUrl}/watch/rescan`, { method: 'POST' })
      if (!res.ok) throw new Error(res.statusText)
      await fetchFiles()
    } catch (error) {
      console.error('Error scanning the downloads folder:', error)
      alert('Failed to scan the downloads folder.')
    } finally {
      isRescanning.value = false
    }
  }

  onMounted(() => {
    fetchFiles()
    pollInterval = setInterval(fetchFiles, 5000)
  })

  onUnmounted(() => {
    if (pollInterval) clearInterval(pollInterval)
  })

  return {
    files,
    isLoading,
    isReidentifying,
    isRescanning,
    pendingRows,
    historyRows,
    fetchFiles,
    reidentifyAll,
    rescanFolder
  }
}

const pad = (n: number) => n.toString().padStart(2, '0')

export const guessLabel = (row: WatchedFile) => {
  const title = row.guess_title || 'Unrecognized'
  if (row.guess_season == null) return title
  if (row.guess_episode == null) return `${title} S${pad(row.guess_season)} (season pack)`
  return `${title} S${pad(row.guess_season)}E${pad(row.guess_episode)}`
}
