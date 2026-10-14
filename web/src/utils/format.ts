export const formatSize = (bytes: number) => {
  if (!bytes || bytes <= 0) return '—'
  const mb = bytes / (1024 * 1024)
  if (mb >= 1024) return (mb / 1024).toFixed(2) + ' GB'
  return mb.toFixed(0) + ' MB'
}

export const formatBytes = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unitIndex = -1
  do {
    value /= 1024
    unitIndex++
  } while (value >= 1024 && unitIndex < units.length - 1)
  return `${value.toFixed(1)} ${units[unitIndex]}`
}

export const formatDate = (dt: string | Date | null | undefined) => {
  if (!dt) return '—'
  const d = new Date(dt as string)
  const yyyy = d.getFullYear()
  const MM = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  const HH = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${yyyy}-${MM}-${dd} ${HH}:${mm}`
}

export const shortPath = (path: string) => path.split('/').slice(-2).join('/')

export const folderOf = (path: string) => path.split('/').slice(0, -1).join('/') || path

/** One-line summary of a collection's ffprobe output: size, bitrate, video codec, audio and subtitle languages. */
export const technicalSummary = (technicalMetadata: string | null | undefined) => {
  if (!technicalMetadata) return null
  try {
    const meta = JSON.parse(technicalMetadata)
    const res: string[] = []
    if (meta.format) {
      if (meta.format.size) {
        const gb = parseInt(meta.format.size) / (1024 * 1024 * 1024)
        res.push(`${gb.toFixed(2)} GB`)
      }
      if (meta.format.bit_rate) {
        const mbps = parseInt(meta.format.bit_rate) / 1000000
        res.push(`${mbps.toFixed(1)} Mbps`)
      }
    }

    if (meta.streams) {
      const video = meta.streams.find((s: any) => s.codec_type === 'video')
      if (video && video.codec_name) res.push(video.codec_name.toUpperCase())

      const languagesOf = (codecType: string) => meta.streams
        .filter((s: any) => s.codec_type === codecType && s.tags && (s.tags.language || s.tags.LANGUAGE))
        .map((s: any) => s.tags.language || s.tags.LANGUAGE)
      const audios = languagesOf('audio')
      if (audios.length > 0) res.push(`Aud: ${[...new Set(audios)].join(',')}`)
      const subs = languagesOf('subtitle')
      if (subs.length > 0) res.push(`Sub: ${[...new Set(subs)].join(',')}`)
    }

    return res.length > 0 ? res.join(' | ') : null
  } catch {
    return null
  }
}

export const collectionLabel = (col: { id: number; name?: string | null; quality?: string | null }) =>
  col.name || col.quality || 'Collection #' + col.id
