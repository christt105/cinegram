import { ref } from 'vue'
import { apiUrl } from '../config'

/** Per-collection actions that run straight away, without a modal. `onChanged` reloads the title. */
export function useCollectionActions(onChanged: () => void) {
  const sendingPreview = ref<number | null>(null)
  const deletingLocalCopy = ref<number | null>(null)

  const sendCollectionPreview = async (collectionId: number) => {
    sendingPreview.value = collectionId
    try {
      const res = await fetch(`${apiUrl}/preview/collection/${collectionId}`, { method: 'POST' })
      if (res.ok) {
        alert('✅ Preview sent to Telegram!')
      } else {
        const err = await res.text()
        alert('❌ Error sending preview: ' + err)
      }
    } catch (err) {
      console.error(err)
      alert('❌ Connection error.')
    } finally {
      sendingPreview.value = null
    }
  }

  const deleteLocalCopy = async (col: any) => {
    if (!confirm(`Delete this file from disk?\n\n${col.local_path}\n\nThe Telegram collection is kept, so you can download it again later.`)) return

    deletingLocalCopy.value = col.id
    try {
      const res = await fetch(`${apiUrl}/local/collection/${col.id}`, { method: 'DELETE' })
      if (res.ok) {
        onChanged()
      } else {
        alert('❌ Error deleting the local copy: ' + await res.text())
      }
    } catch (err) {
      console.error(err)
      alert('❌ Connection error.')
    } finally {
      deletingLocalCopy.value = null
    }
  }

  const deleteCollection = async (collectionId: number) => {
    if (!confirm("Are you sure you want to delete this collection from the database?")) return
    try {
      const res = await fetch(`${apiUrl}/collections/${collectionId}`, { method: 'DELETE' })
      if (res.ok) {
        alert("Collection deleted.")
        onChanged()
      } else {
        alert("Failed to delete collection.")
      }
    } catch (err) {
      console.error(err)
    }
  }

  return { sendingPreview, deletingLocalCopy, sendCollectionPreview, deleteLocalCopy, deleteCollection }
}
