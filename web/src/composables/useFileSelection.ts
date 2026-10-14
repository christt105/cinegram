import { ref, computed, type Ref } from 'vue'
import { apiUrl } from '../config'

/** Selecting files inside one collection of a title, and the batch actions on that selection. */
export function useFileSelection(item: Ref<any>, type: () => string, onChanged: () => void) {
  const selectingCollectionId = ref<number | null>(null)
  const selectedFileIds = ref<number[]>([])

  const isFileSelected = (id: number) => selectedFileIds.value.includes(id)

  const toggleSelectMode = (colId: number) => {
    selectingCollectionId.value = selectingCollectionId.value === colId ? null : colId
    selectedFileIds.value = []
  }

  const toggleFileSelection = (fileId: number) => {
    const idx = selectedFileIds.value.indexOf(fileId)
    if (idx >= 0) selectedFileIds.value.splice(idx, 1)
    else selectedFileIds.value.push(fileId)
  }

  const cancelSelection = () => {
    selectingCollectionId.value = null
    selectedFileIds.value = []
  }

  const allCollections = computed<any[]>(() => {
    if (!item.value) return []
    if (type() === 'movies') return item.value.collections || []
    const all: any[] = []
    for (const season of item.value.seasons || []) {
      all.push(...(season.collections || []))
      for (const ep of season.episodes || []) {
        all.push(...(ep.collections || []))
      }
    }
    return all
  })

  const siblingCollections = computed(() => {
    if (selectingCollectionId.value === null) return []
    return allCollections.value.filter(c => c.id !== selectingCollectionId.value)
  })

  const sourceCollection = computed(() => {
    if (selectingCollectionId.value === null) return null
    return allCollections.value.find(c => c.id === selectingCollectionId.value) || null
  })

  const deleteSelectedFiles = async () => {
    if (!selectedFileIds.value.length) return
    if (!confirm(`Delete ${selectedFileIds.value.length} file(s)? Empty collections will be removed automatically.`)) return
    try {
      const res = await fetch(`${apiUrl}/files/batch-delete`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ file_ids: selectedFileIds.value })
      })
      if (res.ok) {
        cancelSelection()
        onChanged()
      } else {
        alert('Error deleting files.')
      }
    } catch {
      alert('Connection error.')
    }
  }

  const batchMove = (collectionId: number) => fetch(`${apiUrl}/files/batch-move`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ file_ids: selectedFileIds.value, collection_id: collectionId })
  })

  const moveSelectedFiles = async (targetId: number): Promise<boolean> => {
    try {
      const res = await batchMove(targetId)
      if (res.ok) {
        cancelSelection()
        onChanged()
        return true
      }
      alert('Error moving files.')
    } catch {
      alert('Connection error.')
    }
    return false
  }

  const moveSelectedToNewCollection = async (name: string): Promise<boolean> => {
    const source = sourceCollection.value
    if (!source) return false
    try {
      const createRes = await fetch(`${apiUrl}/collections`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          movie_id: source.movie_id ?? null,
          season_id: source.season_id ?? null,
          episode_id: source.episode_id ?? null,
        })
      })
      if (!createRes.ok) {
        alert('Error creating collection.')
        return false
      }
      const newCol = await createRes.json()
      const moveRes = await batchMove(newCol.id)
      if (moveRes.ok) {
        cancelSelection()
        onChanged()
        return true
      }
      alert('Error moving files to new collection.')
    } catch {
      alert('Connection error.')
    }
    return false
  }

  return {
    selectingCollectionId,
    selectedFileIds,
    isFileSelected,
    toggleSelectMode,
    toggleFileSelection,
    cancelSelection,
    siblingCollections,
    deleteSelectedFiles,
    moveSelectedFiles,
    moveSelectedToNewCollection
  }
}
