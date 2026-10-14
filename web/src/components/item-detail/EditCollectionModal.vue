<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px);">
    <div class="glass-panel" style="width: 100%; max-width: 500px; padding: 2rem; border-radius: 16px; background: rgba(17, 24, 39, 0.9); box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5); display: flex; flex-direction: column; gap: 1.25rem; border: 1px solid rgba(255,255,255,0.1);">
      <h3 style="margin-top: 0; font-size: 1.5rem; margin-bottom: 0.5rem; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.5rem;">Edit Collection</h3>

      <div style="display: flex; flex-direction: column; gap: 1rem; max-height: 70vh; overflow-y: auto; padding-right: 0.25rem;">
        <div>
          <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Name / Version (e.g. Extended Version)</label>
          <input v-model="form.name" type="text" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
        </div>

        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Quality</label>
            <select v-model="form.quality" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;">
              <option value="">—</option>
              <option v-for="q in vocabulary.qualities" :key="q" :value="q">{{ q }}</option>
            </select>
          </div>
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Codec</label>
            <select v-model="form.codec" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;">
              <option value="">—</option>
              <option v-for="c in vocabulary.codecs" :key="c" :value="c">{{ c }}</option>
            </select>
          </div>
        </div>

        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Audio Tracks</label>
            <div class="language-chip-picker">
              <button
                v-for="(label, code) in vocabulary.languages"
                :key="'audio-' + code"
                type="button"
                class="language-chip"
                :class="{ active: form.audio_languages.includes(code) }"
                @click="toggleLanguage(form.audio_languages, code)"
              >{{ label }}</button>
            </div>
          </div>
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Subtitles</label>
            <div class="language-chip-picker">
              <button
                v-for="(label, code) in vocabulary.languages"
                :key="'subs-' + code"
                type="button"
                class="language-chip"
                :class="{ active: form.subtitle_languages.includes(code) }"
                @click="toggleLanguage(form.subtitle_languages, code)"
              >{{ label }}</button>
            </div>
          </div>
        </div>

        <div v-if="type === 'series'" class="glass-panel" style="padding: 1rem; background: rgba(255, 255, 255, 0.02); border-radius: 10px; border: 1px solid rgba(255,255,255,0.05); display: flex; flex-direction: column; gap: 0.75rem;">
          <h4 style="margin: 0; font-size: 0.95rem; color: var(--jellyfin-blue); font-weight: 600;">Position within Series</h4>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
            <div>
              <label style="display: block; font-size: 0.8rem; color: #a1a1aa; margin-bottom: 0.25rem;">Season</label>
              <input v-model.number="form.season_number" type="number" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
            </div>

            <div v-if="!form.is_season_pack">
              <label style="display: block; font-size: 0.8rem; color: #a1a1aa; margin-bottom: 0.25rem;">Episode</label>
              <input v-model.number="form.episode_number" type="number" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
            </div>
          </div>

          <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 0.25rem;">
            <input type="checkbox" id="is_season_pack" v-model="form.is_season_pack" style="width: 16px; height: 16px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
            <label for="is_season_pack" style="font-size: 0.85rem; color: #d1d5db; cursor: pointer;">Is a Full Season Pack</label>
          </div>
        </div>

        <div>
          <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Notes</label>
          <textarea v-model="form.notes" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; min-height: 60px; font-family: sans-serif; resize: vertical;"></textarea>
        </div>
      </div>

      <div style="display: flex; justify-content: flex-end; gap: 0.75rem; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 1rem; margin-top: 0.5rem;">
        <button @click="emit('close')" class="glass-button">Cancel</button>
        <button @click="save" class="glass-button primary">Save Changes</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiUrl } from '../../config'
import type { MetadataVocabulary, TelegramCollection } from '../../api/cinegram'

const props = defineProps<{
  collection: TelegramCollection
  type: string
  seasonNumber: number | null
  episodeNumber: number | null
  vocabulary: MetadataVocabulary
}>()

const emit = defineEmits<{
  close: []
  saved: []
}>()

const splitLanguages = (value: string | null | undefined): string[] =>
  (value || '').split(',').map(c => c.trim()).filter(Boolean)

const form = ref({
  name: props.collection.name || '',
  quality: props.collection.quality || '',
  codec: props.collection.codec || '',
  audio_languages: splitLanguages(props.collection.audio_languages),
  subtitle_languages: splitLanguages(props.collection.subtitle_languages),
  notes: props.collection.notes || '',
  season_number: props.seasonNumber !== null ? props.seasonNumber : 1,
  episode_number: props.episodeNumber,
  is_season_pack: props.episodeNumber === null && props.seasonNumber !== null
})

const toggleLanguage = (list: string[], code: string) => {
  const idx = list.indexOf(code)
  if (idx === -1) list.push(code)
  else list.splice(idx, 1)
}

const save = async () => {
  try {
    const payload: Record<string, string | number | boolean | null> = {
      name: form.value.name,
      quality: form.value.quality,
      codec: form.value.codec,
      audio_languages: form.value.audio_languages.join(','),
      subtitle_languages: form.value.subtitle_languages.join(','),
      notes: form.value.notes
    }

    if (props.type === 'series') {
      payload.season_number = form.value.season_number
      if (form.value.is_season_pack) {
        payload.clear_episode = true
        payload.episode_number = null
      } else {
        payload.episode_number = form.value.episode_number
        payload.clear_episode = false
      }
    }

    const res = await fetch(`${apiUrl}/collections/${props.collection.id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })

    if (res.ok) {
      alert("Collection updated successfully.")
      emit('saved')
    } else {
      alert("Error saving changes.")
    }
  } catch (err) {
    console.error(err)
    alert("Connection error.")
  }
}
</script>

<style scoped>
.language-chip-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  max-height: 140px;
  overflow-y: auto;
  padding: 0.5rem;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
}
.language-chip {
  padding: 3px 10px;
  border-radius: var(--r-full);
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #d1d5db;
  font-size: 0.78rem;
  cursor: pointer;
  transition: all 0.15s;
}
.language-chip.active {
  background: rgba(96, 165, 250, 0.2);
  border-color: rgba(96, 165, 250, 0.5);
  color: #93c5fd;
}
</style>
