<template>
  <div class="item-detail-page">
    <div class="header-nav">
      <button @click="$router.back()" class="glass-button">
        &larr; Back
      </button>
      <div style="flex-grow:1"></div>
    </div>

    <div v-if="isLoading" class="loading-state">Loading...</div>
    <div v-else-if="item" class="item-content">
      
      <div class="item-hero glass-panel">
        <div
          v-if="item.poster_path"
          class="hero-backdrop"
          :style="{ backgroundImage: `url(${item.poster_path.startsWith('/') ? 'https://image.tmdb.org/t/p/w780' + item.poster_path : item.poster_path})` }"
        ></div>
        <img v-if="item.poster_path" :src="item.poster_path.startsWith('/') ? 'https://image.tmdb.org/t/p/w500' + item.poster_path : item.poster_path" class="hero-poster" />
        <div class="hero-info">
          <div style="display: flex; align-items: center; gap: 1rem; flex-wrap: wrap;">
            <h1 style="margin: 0; font-size: 2.2rem; font-weight: 700;">{{ item.title || item.manual_title || 'Unknown Title' }}</h1>
            <span class="year-badge" style="background: rgba(255,255,255,0.1); padding: 0.25rem 0.6rem; border-radius: 8px; font-size: 0.9rem; font-weight: 600; border: 1px solid rgba(255,255,255,0.15);">{{ item.release_year }}</span>
            <div v-if="item.tmdb_id" style="display: inline-flex; align-items: center; gap: 0.35rem;">
              <a :href="type === 'movies' ? 'https://www.themoviedb.org/movie/' + item.tmdb_id : 'https://www.themoviedb.org/tv/' + item.tmdb_id" target="_blank" rel="noopener noreferrer" class="tmdb-link" style="display: inline-flex; align-items: center; gap: 0.4rem; background: rgba(13, 37, 63, 0.65); border: 1px solid rgba(1, 180, 228, 0.4); padding: 0.25rem 0.6rem; border-radius: 8px; font-size: 0.85rem; font-weight: 600; color: #90cea1; text-decoration: none; transition: all 0.2s; height: 30px;">
                <span style="color: #01b4e4; font-weight: 800;">TMDB:</span> {{ item.tmdb_id }}
              </a>
              <button @click="copyTmdbId" class="glass-button" style="padding: 0; border-radius: 8px; display: inline-flex; align-items: center; justify-content: center; height: 30px; width: 30px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.1); cursor: pointer;" title="Copy TMDB ID">
                <Check v-if="copied" :size="13" style="color: #4ade80;" />
                <Copy v-else :size="13" style="color: #a1a1aa;" />
              </button>
            </div>
          </div>
          <p class="overview" style="margin-top: 1rem; color: #d1d5db; line-height: 1.6;">{{ item.overview }}</p>
          <div class="admin-actions" style="margin-top: 1.5rem; display: flex; gap: 0.75rem; flex-wrap: wrap;">
            <router-link v-if="jellyfinItemId" :to="'/jellyfin/' + type + '/' + jellyfinItemId" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; text-decoration: none; display: inline-flex; align-items: center; gap: 0.5rem; padding: 6px 12px; font-size: 0.9rem; border-radius: 8px;">
              📂 View Jellyfin Card
            </router-link>
            <button @click="reidentifyItem" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;">
              🔍 Re-identify (TMDB ID)
            </button>
            <button @click="changePoster" class="glass-button" style="background: rgba(34, 197, 94, 0.14); border-color: rgba(34, 197, 94, 0.30); color: #7ee2a8;">
              🖼️ Change Poster
            </button>
            <button @click="deleteItem" class="glass-button danger" style="background: rgba(255, 180, 171, 0.12); border-color: rgba(255, 180, 171, 0.30); color: #ffb4ab;">
              🗑️ Delete All
            </button>
          </div>
        </div>
      </div>

      <div class="item-actions">
        <!-- Movies have collections directly -->
        <div v-if="type === 'movies'" class="collections-section">
          <h2>Available Versions</h2>
          <div v-if="item.collections && item.collections.length > 0" class="collection-list">
            <div v-for="col in item.collections" :key="col.id" class="collection-item glass-panel">
              <div class="col-info" style="display: flex; flex-direction: column; gap: 0.5rem;">
                <strong style="font-size: 1.1rem; color: #4ade80;">{{ col.name || col.quality || 'Full Backup' }}</strong>
                <span v-if="col.audio_languages" style="font-size: 0.9rem; color: #a1a1aa;">Audio: {{ col.audio_languages }}</span>
                <span v-if="getTechMeta(col)" style="font-size: 0.8rem; color: #a1a1aa; max-width: 100%;">{{ getTechMeta(col) }}</span>
                <span v-else-if="col.technical_metadata" class="meta-badge">Technical Info Available</span>
              </div>
              <div class="col-actions" style="display: flex; gap: 0.5rem; flex-wrap: wrap; justify-content: flex-end; align-items: center;">
                <button @click="sendCollectionPreview(col.id)" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" :disabled="sendingPreview === col.id" :title="'Send Info & Files to Telegram'">
                  <span v-if="sendingPreview === col.id">⏳</span>
                  <span v-else>📨</span>
                  <span style="margin-left: 4px;">{{ sendingPreview === col.id ? 'Sending...' : 'Send' }}</span>
                </button>
                <button @click="downloadCollection(col.id)" class="glass-button primary">
                  <DownloadCloud :size="16" /> Download
                </button>
                <button @click="openReidentifyCollection(col)" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;">
                  <Search :size="16" /> Re-identify
                </button>
                <button @click="openEditModal(col, null, null)" class="glass-button" style="background: rgba(255, 255, 255, 0.08); border-color: rgba(255, 255, 255, 0.15); color: #fff;">
                  <Edit3 :size="16" /> Edit
                </button>
                <button @click="deleteCollection(col.id)" class="glass-button danger">
                  <Trash2 :size="16" /> Delete
                </button>
              </div>
            </div>
          </div>
          <div v-else>No versions uploaded to Telegram yet.</div>
        </div>

        <!-- Series have seasons and episodes -->
        <div v-if="type === 'series'" class="seasons-section">
          <h2 style="margin-bottom: 1.5rem; font-size: 2rem;">Seasons</h2>
          <div v-for="season in item.seasons" :key="season.id" class="season-block">
            
            <div class="season-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid rgba(255,255,255,0.1); flex-wrap: wrap; gap: 1rem;">
              <h3 style="font-size: 1.5rem; margin: 0;">Season {{ season.season_number }}</h3>
              <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
                <button @click="sendSeasonPreview(season.season_number)" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" :disabled="sendingSeasonPreview === season.season_number" :title="'Send season Info & Files to Telegram'">
                  <span v-if="sendingSeasonPreview === season.season_number">⏳</span>
                  <span v-else>📨</span>
                  <span style="margin-left: 4px;">{{ sendingSeasonPreview === season.season_number ? 'Sending...' : 'Send' }}</span>
                </button>
                <button @click="downloadSeason(season.season_number)" class="glass-button primary">
                  <DownloadCloud :size="16" /> Download Season
                </button>
              </div>
            </div>

            <!-- Season Packs (Collections linked directly to Season) -->
            <div v-if="season.collections && season.collections.length > 0" class="season-packs-section" style="margin-bottom: 1.5rem;">
              <h4 style="margin: 0 0 0.75rem 0; font-size: 1rem; color: #4ade80; display: flex; align-items: center; gap: 0.5rem;">
                📦 Full Season Packs
              </h4>
              <div class="season-packs-list" style="display: flex; flex-direction: column; gap: 0.75rem;">
                <div v-for="col in season.collections" :key="col.id" class="collection-item glass-panel" style="padding: 1rem; background: rgba(0, 0, 0, 0.25); border-radius: 12px; display: flex; justify-content: space-between; align-items: center; border: 1px solid rgba(74, 222, 128, 0.15);">
                  <div style="display: flex; flex-direction: column; gap: 0.25rem;">
                    <strong style="color: #4ade80; font-size: 1.05rem;">{{ col.name || col.quality || 'Full Season' }}</strong>
                    <span v-if="col.audio_languages" style="font-size: 0.85rem; color: #a1a1aa;">Audio: {{ col.audio_languages }}</span>
                    <span v-if="getTechMeta(col)" style="font-size: 0.8rem; color: #888;">{{ getTechMeta(col) }}</span>
                  </div>
                  <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
                    <button @click="sendCollectionPreview(col.id)" class="glass-button btn-sm" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" :disabled="sendingPreview === col.id" title="Send to Telegram">
                      <span>{{ sendingPreview === col.id ? '⏳' : '📨' }}</span>
                    </button>
                    <button @click="downloadCollection(col.id)" class="glass-button primary btn-sm">
                      <DownloadCloud :size="14" /> Download Pack
                    </button>
                    <button @click="openReidentifyCollection(col)" class="glass-button btn-sm" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;">
                      <Search :size="14" /> Re-id
                    </button>
                    <button @click="openEditModal(col, season.season_number, null)" class="glass-button btn-sm" style="background: rgba(255, 255, 255, 0.08); border-color: rgba(255, 255, 255, 0.15); color: #fff;">
                      <Edit3 :size="14" /> Edit
                    </button>
                    <button @click="deleteCollection(col.id)" class="glass-button danger btn-sm">
                      <Trash2 :size="14" /> Delete
                    </button>
                  </div>
                </div>
              </div>
            </div>
            
            <div class="episode-list">
              <div v-for="ep in season.episodes" :key="ep.id" class="episode-item glass-panel" style="flex-direction: column; align-items: stretch; gap: 0;">
                <div class="ep-info" style="border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 0.75rem; margin-bottom: 0.75rem;">
                  <strong style="color: var(--jellyfin-blue);">E{{ ep.episode_number }}</strong>
                  <span style="font-weight: 500;">{{ ep.title?.replace(/^Episode\s+\d+$/, '') || 'Episode ' + ep.episode_number }}</span>
                </div>
                
                <div v-if="ep.collections && ep.collections.length > 0" class="ep-collections" style="width: 100%;">
                  <div v-for="col in ep.collections" :key="col.id" class="collection-item" style="background: rgba(0,0,0,0.2); padding: 0.5rem; border-radius: 8px; margin-bottom: 0.5rem; border: 1px solid rgba(255,255,255,0.03);">
                    <div style="display: flex; flex-direction: column;">
                      <span style="font-size: 0.85rem; color: #d1d5db; font-weight: 500;">{{ col.name || col.quality || 'Auto' }}</span>
                      <span style="font-size: 0.75rem; color: #888;">{{ col.files?.length || 0 }} files</span>
                    </div>
                    <div style="display: flex; gap: 0.25rem; flex-wrap: wrap; justify-content: flex-end;">
                      <button @click="downloadCollection(col.id)" class="glass-button primary btn-sm icon-only" title="Download">
                        <DownloadCloud :size="14" />
                      </button>
                      <button @click="openReidentifyCollection(col)" class="glass-button btn-sm icon-only" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" title="Re-identify">
                        <Search :size="14" />
                      </button>
                      <button @click="openEditModal(col, season.season_number, ep.episode_number)" class="glass-button btn-sm icon-only" style="background: rgba(255, 255, 255, 0.08); border-color: rgba(255, 255, 255, 0.15); color: #fff;" title="Edit">
                        <Edit3 :size="14" />
                      </button>
                      <button @click="deleteCollection(col.id)" class="glass-button danger btn-sm icon-only" title="Delete">
                        <Trash2 :size="14" />
                      </button>
                    </div>
                  </div>
                </div>
                <div v-else class="no-col" style="color: var(--text-secondary); font-size: 0.85rem;">Not backed up yet</div>
              </div>
            </div>
          </div>
        </div>
        
      </div>
    </div>

    <!-- Edit Collection Modal -->
    <div v-if="editingCollection" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px);">
      <div class="glass-panel" style="width: 100%; max-width: 500px; padding: 2rem; border-radius: 16px; background: rgba(17, 24, 39, 0.9); box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5); display: flex; flex-direction: column; gap: 1.25rem; border: 1px solid rgba(255,255,255,0.1);">
        <h3 style="margin-top: 0; font-size: 1.5rem; margin-bottom: 0.5rem; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.5rem;">Edit Collection</h3>
        
        <div style="display: flex; flex-direction: column; gap: 1rem; max-height: 70vh; overflow-y: auto; padding-right: 0.25rem;">
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Name / Version (e.g. Extended Version)</label>
            <input v-model="editForm.name" type="text" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
          </div>
          
          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Quality (e.g. 1080p, 4K, HDR)</label>
            <input v-model="editForm.quality" type="text" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
            <div>
              <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Audio Tracks (e.g. es, en)</label>
              <input v-model="editForm.audio_languages" type="text" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
            </div>
            <div>
              <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Subtitles</label>
              <input v-model="editForm.subtitle_languages" type="text" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
            </div>
          </div>

          <!-- Series season/episode change controls -->
          <div v-if="type === 'series'" class="glass-panel" style="padding: 1rem; background: rgba(255, 255, 255, 0.02); border-radius: 10px; border: 1px solid rgba(255,255,255,0.05); display: flex; flex-direction: column; gap: 0.75rem;">
            <h4 style="margin: 0; font-size: 0.95rem; color: var(--jellyfin-blue); font-weight: 600;">Position within Series</h4>
            
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
              <div>
                <label style="display: block; font-size: 0.8rem; color: #a1a1aa; margin-bottom: 0.25rem;">Season</label>
                <input v-model.number="editForm.season_number" type="number" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
              </div>
              
              <div v-if="!editForm.is_season_pack">
                <label style="display: block; font-size: 0.8rem; color: #a1a1aa; margin-bottom: 0.25rem;">Episode</label>
                <input v-model.number="editForm.episode_number" type="number" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
              </div>
            </div>

            <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 0.25rem;">
              <input type="checkbox" id="is_season_pack" v-model="editForm.is_season_pack" style="width: 16px; height: 16px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
              <label for="is_season_pack" style="font-size: 0.85rem; color: #d1d5db; cursor: pointer;">Is a Full Season Pack</label>
            </div>
          </div>

          <div>
            <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Notes</label>
            <textarea v-model="editForm.notes" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; min-height: 60px; font-family: sans-serif; resize: vertical;"></textarea>
          </div>
        </div>

        <div style="display: flex; justify-content: flex-end; gap: 0.75rem; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 1rem; margin-top: 0.5rem;">
          <button @click="editingCollection = null" class="glass-button">Cancel</button>
          <button @click="saveCollectionChanges" class="glass-button primary">Save Changes</button>
        </div>
      </div>
    </div>

    <!-- Choose Poster Modal -->
    <div v-if="showingPosterModal" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px);">
      <div class="glass-panel" style="width: 100%; max-width: 600px; padding: 2rem; border-radius: 16px; background: rgba(17, 24, 39, 0.9); box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5); display: flex; flex-direction: column; gap: 1.25rem; border: 1px solid rgba(255,255,255,0.1);">
        <h3 style="margin-top: 0; font-size: 1.5rem; margin-bottom: 0.5rem; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.5rem;">Choose Poster from TMDB</h3>
        
        <!-- List of TMDB posters -->
        <div v-if="loadingPosters" style="color: #a1a1aa; text-align: center; padding: 2rem;">
          Loading available TMDB posters...
        </div>
        <div v-else-if="availablePosters.length === 0" style="color: #a1a1aa; text-align: center; padding: 1.5rem;">
          No TMDB posters found for this item.
        </div>
        <div v-else class="posters-grid" style="display: grid; grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); gap: 0.75rem; max-height: 45vh; overflow-y: auto; padding-right: 0.5rem;">
          <div v-for="path in availablePosters" :key="path" @click="selectPoster(path)" class="poster-option" style="cursor: pointer; border-radius: 8px; overflow: hidden; border: 3px solid transparent; transition: all 0.2s; position: relative;" :style="{ borderColor: item.poster_path === path ? 'var(--jellyfin-blue)' : 'transparent' }">
            <img :src="'https://image.tmdb.org/t/p/w185' + path" style="width: 100%; display: block;" />
            <div v-if="item.poster_path === path" style="position: absolute; top: 4px; right: 4px; background: var(--jellyfin-blue); color: white; border-radius: 50%; width: 20px; height: 20px; display: flex; align-items: center; justify-content: center; font-size: 0.75rem; font-weight: bold;">✓</div>
          </div>
        </div>

        <!-- Manual custom URL fallback -->
        <div style="border-top: 1px solid rgba(255,255,255,0.08); padding-top: 1rem; display: flex; flex-direction: column; gap: 0.5rem;">
          <label style="font-size: 0.85rem; color: #a1a1aa;">Or enter a poster URL manually:</label>
          <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
            <input v-model="manualPosterUrl" type="text" placeholder="https://... o /path.jpg" style="flex-grow: 1; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
            <button @click="selectPoster(manualPosterUrl)" class="glass-button primary">Apply</button>
          </div>
        </div>

        <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 1rem; margin-top: 0.5rem;">
          <button @click="showingPosterModal = false" class="glass-button">Close</button>
        </div>
      </div>
    </div>

    <!-- TMDB Reidentify Modal -->
    <div v-if="showingReidentifyModal" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
      <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">
        
        <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
          <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Re-identify in TMDB</h3>
          <button @click="showingReidentifyModal = false" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px;">✕</button>
        </div>
        
        <!-- Search Bar -->
        <div style="display: flex; gap: 0.5rem; margin-bottom: 0.5rem;">
          <input v-model="searchQueryTMDB" @keyup.enter="searchTMDB" type="text" placeholder="Type the series or movie name..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
          <button @click="searchTMDB" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
        </div>

        <!-- Results List -->
        <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem; min-height: 200px;">
          <div v-if="isSearchingTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            Searching TMDB...
          </div>
          <div v-else-if="searchResultsTMDB.length === 0 && searchQueryTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            No results found.
          </div>
          <div v-else-if="searchResultsTMDB.length === 0" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            Type a title above and press Search.
          </div>
          
          <div v-else v-for="result in searchResultsTMDB" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
            <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" style="width: 50px; height: 75px; object-fit: cover; border-radius: 6px; flex-shrink: 0;" />
            <div v-else style="width: 50px; height: 75px; background: rgba(255,255,255,0.05); border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 1.5rem; color: #4b5563; flex-shrink: 0;">🎬</div>
            
            <div style="flex-grow: 1; display: flex; flex-direction: column; gap: 0.25rem; min-width: 0;">
              <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
                <strong style="color: #fff; text-overflow: ellipsis; overflow: hidden; white-space: nowrap; max-width: 250px;">{{ result.title }}</strong>
                <span style="font-size: 0.75rem; background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 4px; color: #d1d5db;">{{ result.year }}</span>
                <span :style="{ background: result.media_type === 'movie' ? 'rgba(214, 186, 255, 0.14)' : 'rgba(214, 186, 255, 0.14)', color: result.media_type === 'movie' ? '#d6baff' : '#d6baff' }" style="font-size: 0.7rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; text-transform: uppercase;">
                  {{ result.media_type === 'movie' ? 'Movie' : 'Series' }}
                </span>
              </div>
              <p style="margin: 0; font-size: 0.8rem; color: #a1a1aa; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; text-overflow: ellipsis; line-height: 1.4;">{{ result.overview }}</p>
            </div>
            
            <button @click="selectTMDBReidentify(result.id)" class="glass-button" style="align-self: center; background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
              Select
            </button>
          </div>
        </div>
        
        <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
          <button @click="showingReidentifyModal = false" class="glass-button" style="padding: 6px 16px;">Close</button>
        </div>
      </div>
    </div>

    <!-- Re-identify Collection Modal -->
    <div v-if="reidentifyCollectionModal.open" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
      <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(214, 186, 255, 0.20); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">

        <div style="display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
          <div>
            <h3 style="margin: 0 0 0.25rem 0; font-size: 1.2rem; color: #fff;">🔍 Re-identify Collection</h3>
            <p style="margin: 0; font-size: 0.85rem; color: #d6baff;">{{ reidentifyCollectionModal.name }}</p>
          </div>
          <button @click="reidentifyCollectionModal.open = false" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px; flex-shrink: 0;">✕</button>
        </div>

        <!-- Search Bar -->
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
          <input v-model="searchQueryTMDB" @keyup.enter="searchTMDB" type="text" placeholder="Type the movie or series name..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
          <button @click="searchTMDB" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
        </div>

        <!-- Results List -->
        <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem; min-height: 150px;">
          <div v-if="isSearchingTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">Searching TMDB...</div>
          <div v-else-if="searchResultsTMDB.length === 0 && searchQueryTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">No results found.</div>
          <div v-else-if="searchResultsTMDB.length === 0" style="text-align: center; padding: 1.5rem; color: #a1a1aa;">Type a title above and press Search.</div>

          <div v-else v-for="result in searchResultsTMDB" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
            <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" style="width: 50px; height: 75px; object-fit: cover; border-radius: 6px; flex-shrink: 0;" />
            <div v-else style="width: 50px; height: 75px; background: rgba(255,255,255,0.05); border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 1.5rem; color: #4b5563; flex-shrink: 0;">🎬</div>

            <div style="flex-grow: 1; display: flex; flex-direction: column; gap: 0.25rem; min-width: 0;">
              <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
                <strong style="color: #fff; text-overflow: ellipsis; overflow: hidden; white-space: nowrap; max-width: 250px;">{{ result.title }}</strong>
                <span style="font-size: 0.75rem; background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 4px; color: #d1d5db;">{{ result.year }}</span>
                <span :style="{ background: result.media_type === 'movie' ? 'rgba(214, 186, 255, 0.14)' : 'rgba(214, 186, 255, 0.14)', color: result.media_type === 'movie' ? '#d6baff' : '#d6baff' }" style="font-size: 0.7rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; text-transform: uppercase;">{{ result.media_type === 'movie' ? 'Movie' : 'Series' }}</span>
              </div>
              <p style="margin: 0; font-size: 0.8rem; color: #a1a1aa; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">{{ result.overview }}</p>
            </div>

            <button @click="selectCollectionTMDB(result.id)" :disabled="reidentifyCollectionModal.loading" class="glass-button" style="align-self: center; background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
              Select
            </button>
          </div>
        </div>

        <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
          <button @click="reidentifyCollectionModal.open = false" class="glass-button" style="padding: 6px 16px;">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { DownloadCloud, Trash2, Edit3, Copy, Check, Search } from 'lucide-vue-next'
import { findJellyfinItemByTmdbId } from '../api/jellyfin'

const props = defineProps<{
  type: string
  id: string
}>()

const item = ref<any>(null)
const isLoading = ref(true)

const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`

const editingCollection = ref<any>(null)
const editForm = ref({
  id: 0,
  name: '',
  quality: '',
  audio_languages: '',
  subtitle_languages: '',
  notes: '',
  season_number: 1,
  episode_number: null as number | null,
  is_season_pack: false
})

const showingPosterModal = ref(false)
const loadingPosters = ref(false)
const availablePosters = ref<string[]>([])
const manualPosterUrl = ref('')

const showingReidentifyModal = ref(false)
const searchQueryTMDB = ref('')
const searchResultsTMDB = ref<any[]>([])
const isSearchingTMDB = ref(false)

const copied = ref(false)
const copyTmdbId = () => {
  if (!item.value?.tmdb_id) return
  const text = item.value.tmdb_id.toString()
  
  let success = false
  if (navigator.clipboard && window.isSecureContext) {
    navigator.clipboard.writeText(text)
    success = true
  } else {
    // Fallback using textarea for non-secure HTTP local network contexts
    const textArea = document.createElement("textarea")
    textArea.value = text
    textArea.style.position = "fixed"
    textArea.style.left = "-999999px"
    textArea.style.top = "-999999px"
    document.body.appendChild(textArea)
    textArea.focus()
    textArea.select()
    try {
      document.execCommand('copy')
      success = true
    } catch (err) {
      console.error('Fallback copy failed', err)
    }
    textArea.remove()
  }
  
  if (success) {
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } else {
    alert("Could not copy. Please copy it manually.")
  }
}

const fetchItem = async () => {
  isLoading.value = true
  try {
    const endpoint = props.type === 'movies' ? `/movies/${props.id}` : `/series/${props.id}`
    const res = await fetch(`${backendUrl}${endpoint}`)
    if (res.ok) {
      const data = await res.json()
      if (data.seasons) {
        data.seasons.sort((a: any, b: any) => a.season_number - b.season_number)
        data.seasons.forEach((season: any) => {
          if (season.episodes) {
            season.episodes.sort((a: any, b: any) => a.episode_number - b.episode_number)
          }
        })
      }
      item.value = data
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

const jellyfinItemId = ref<string | null>(null)
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

const openEditModal = (col: any, currentSeason: number | null, currentEpisode: number | null) => {
  editingCollection.value = col
  editForm.value = {
    id: col.id,
    name: col.name || '',
    quality: col.quality || '',
    audio_languages: col.audio_languages || '',
    subtitle_languages: col.subtitle_languages || '',
    notes: col.notes || '',
    season_number: currentSeason !== null ? currentSeason : 1,
    episode_number: currentEpisode,
    is_season_pack: currentEpisode === null && currentSeason !== null
  }
}

const saveCollectionChanges = async () => {
  try {
    const payload: any = {
      name: editForm.value.name,
      quality: editForm.value.quality,
      audio_languages: editForm.value.audio_languages,
      subtitle_languages: editForm.value.subtitle_languages,
      notes: editForm.value.notes
    }

    if (props.type === 'series') {
      payload.season_number = editForm.value.season_number
      if (editForm.value.is_season_pack) {
        payload.clear_episode = true
        payload.episode_number = null
      } else {
        payload.episode_number = editForm.value.episode_number
        payload.clear_episode = false
      }
    }

    const res = await fetch(`${backendUrl}/collections/${editForm.value.id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })

    if (res.ok) {
      alert("Collection updated successfully.")
      editingCollection.value = null
      fetchItem() // refresh to reload layout and locations
    } else {
      alert("Error saving changes.")
    }
  } catch (err) {
    console.error(err)
    alert("Connection error.")
  }
}

const downloadCollection = async (collectionId: number) => {
  try {
    const res = await fetch(`${backendUrl}/downloads/enqueue/collection/${collectionId}`, { method: 'POST' })
    if (res.ok) {
      alert("Download added to queue.")
    } else {
      alert("Error adding download.")
    }
  } catch (err) {
    console.error(err)
  }
}

const downloadSeason = async (seasonNumber: number) => {
  try {
    const res = await fetch(`${backendUrl}/downloads/enqueue/series/${props.id}/season/${seasonNumber}`, { method: 'POST' })
    if (res.ok) {
      alert("Full season added to queue.")
    } else {
      alert("Error downloading season.")
    }
  } catch (err) {
    console.error(err)
  }
}

const botNetUrl = import.meta.env.VITE_BOT_NET_URL || `${window.location.protocol}//${window.location.hostname}:8088`

const sendingPreview = ref<number | null>(null)
const sendingSeasonPreview = ref<number | null>(null)

const sendCollectionPreview = async (collectionId: number) => {
  sendingPreview.value = collectionId
  try {
    const res = await fetch(`${botNetUrl}/preview/collection/${collectionId}`, { method: 'POST' })
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

const sendSeasonPreview = async (seasonNumber: number) => {
  sendingSeasonPreview.value = seasonNumber
  try {
    const res = await fetch(`${botNetUrl}/preview/series/${props.id}/season/${seasonNumber}`, { method: 'POST' })
    if (res.ok) {
      alert('✅ Season preview sent to Telegram!')
    } else {
      const err = await res.text()
      alert('❌ Error sending season preview: ' + err)
    }
  } catch (err) {
    console.error(err)
    alert('❌ Connection error.')
  } finally {
    sendingSeasonPreview.value = null
  }
}

const getTechMeta = (col: any) => {
    if (!col.technical_metadata) return null;
    try {
        const meta = JSON.parse(col.technical_metadata);
        let res = [];
        if (meta.format) {
            if (meta.format.size) {
                const gb = parseInt(meta.format.size) / (1024 * 1024 * 1024);
                res.push(`${gb.toFixed(2)} GB`);
            }
            if (meta.format.bit_rate) {
                const mbps = parseInt(meta.format.bit_rate) / 1000000;
                res.push(`${mbps.toFixed(1)} Mbps`);
            }
        }
        
        if (meta.streams) {
            const video = meta.streams.find((s:any) => s.codec_type === 'video');
            if (video && video.codec_name) res.push(video.codec_name.toUpperCase());
            
            const audios = meta.streams
                .filter((s:any) => s.codec_type === 'audio' && s.tags && (s.tags.language || s.tags.LANGUAGE))
                .map((s:any) => s.tags.language || s.tags.LANGUAGE);
            if (audios.length > 0) res.push(`Aud: ${[...new Set(audios)].join(',')}`);
            
            const subs = meta.streams
                .filter((s:any) => s.codec_type === 'subtitle' && s.tags && (s.tags.language || s.tags.LANGUAGE))
                .map((s:any) => s.tags.language || s.tags.LANGUAGE);
            if (subs.length > 0) res.push(`Sub: ${[...new Set(subs)].join(',')}`);
        }
        
        return res.length > 0 ? res.join(' | ') : null;
    } catch (e) {
        return null;
    }
}

const deleteCollection = async (collectionId: number) => {
  if (!confirm("Are you sure you want to delete this collection from the database?")) return;
  try {
    const res = await fetch(`${backendUrl}/collections/${collectionId}`, { method: 'DELETE' })
    if (res.ok) {
      alert("Collection deleted.")
      fetchItem() // refresh
    } else {
      alert("Failed to delete collection.")
    }
  } catch (err) {
    console.error(err)
  }
}

const changePoster = async () => {
  showingPosterModal.value = true
  loadingPosters.value = true
  manualPosterUrl.value = item.value.poster_path || ''
  availablePosters.value = []
  try {
    const endpoint = props.type === 'movies' ? `/movies/${props.id}/posters` : `/series/${props.id}/posters`
    const res = await fetch(`${backendUrl}${endpoint}`)
    if (res.ok) {
      availablePosters.value = await res.json()
    }
  } catch (err) {
    console.error(err)
  } finally {
    loadingPosters.value = false
  }
}

const selectPoster = async (path: string) => {
  if (!path.trim()) return
  
  try {
    const endpoint = props.type === 'movies' ? `/movies/${props.id}` : `/series/${props.id}`
    const res = await fetch(`${backendUrl}${endpoint}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ poster_path: path.trim() })
    })
    if (res.ok) {
      alert("Poster updated successfully.")
      showingPosterModal.value = false
      fetchItem()
    } else {
      alert("Error updating the poster.")
    }
  } catch (err) {
    console.error(err)
    alert("Connection error.")
  }
}

const reidentifyItem = () => {
  searchQueryTMDB.value = item.value?.title || item.value?.manual_title || ''
  searchResultsTMDB.value = []
  showingReidentifyModal.value = true
}

const reidentifyCollectionModal = ref({
  open: false,
  collectionId: 0,
  name: '',
  loading: false
})

const openReidentifyCollection = (col: any) => {
  reidentifyCollectionModal.value = { open: true, collectionId: col.id, name: col.name, loading: false }
  searchQueryTMDB.value = col.name
  searchResultsTMDB.value = []
}


const selectCollectionTMDB = async (tmdbId: number) => {
  reidentifyCollectionModal.value.loading = true
  try {
    const res = await fetch(`${backendUrl}/collections/${reidentifyCollectionModal.value.collectionId}/reidentify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tmdb_id: tmdbId })
    })
    if (res.ok) {
      reidentifyCollectionModal.value.open = false
      alert('✅ Collection re-identified successfully.')
      fetchItem()
    } else {
      const errText = await res.text()
      alert('❌ Error re-identifying: ' + errText)
    }
  } catch (err) {
    console.error(err)
    alert('Connection error.')
  } finally {
    reidentifyCollectionModal.value.loading = false
  }
}

const searchTMDB = async () => {
  if (!searchQueryTMDB.value.trim()) return
  isSearchingTMDB.value = true
  searchResultsTMDB.value = []
  try {
    const res = await fetch(`${backendUrl}/tmdb/search?query=${encodeURIComponent(searchQueryTMDB.value.trim())}`)
    if (res.ok) {
      searchResultsTMDB.value = await res.json()
    }
  } catch (err) {
    console.error(err)
  } finally {
    isSearchingTMDB.value = false
  }
}

const selectTMDBReidentify = async (newTmdbId: number) => {
  showingReidentifyModal.value = false
  isLoading.value = true
  try {
    const endpoint = props.type === 'movies' 
      ? `/movies/${props.id}/reidentify?new_tmdb_id=${newTmdbId}` 
      : `/series/${props.id}/reidentify?new_tmdb_id=${newTmdbId}`
    const res = await fetch(`${backendUrl}${endpoint}`, { method: 'POST' })
    if (res.ok) {
      alert("Item re-identified successfully. Info has been updated and files re-mapped.")
      fetchItem() // refresh
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

const deleteItem = async () => {
  const confirmMsg = props.type === 'movies'
    ? "Are you sure you want to delete this movie? All collections will be unlinked."
    : "Are you sure you want to delete this series? All seasons and episodes will be unlinked."
  if (!confirm(confirmMsg)) return
  
  try {
    const endpoint = props.type === 'movies' ? `/movies/${props.id}` : `/series/${props.id}`
    const res = await fetch(`${backendUrl}${endpoint}`, { method: 'DELETE' })
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

onMounted(() => {
  fetchItem()
})
</script>

<style scoped>
.item-detail-page {
  padding: 2rem;
  max-width: 1040px;
  margin: 0 auto;
  overflow-wrap: anywhere;
  word-break: break-word;
}
.header-nav {
  margin-bottom: 1.5rem;
}
.hero-poster {
  position: relative;
  z-index: 1;
  width: 240px;
  border-radius: var(--r-xl);
  box-shadow: 0 20px 40px rgba(0,0,0,0.6);
  border: 1px solid var(--glass-border);
}
.item-hero {
  position: relative;
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
  padding: 1.5rem;
  border-radius: var(--r-2xl);
  overflow: hidden;
}
.hero-backdrop {
  position: absolute;
  inset: 0;
  background-size: cover;
  background-position: center;
  filter: blur(24px) brightness(0.4);
  transform: scale(1.1);
  z-index: 0;
}
.item-hero > .hero-info,
.item-hero > .hero-poster {
  position: relative;
  z-index: 1;
}
.hero-info {
  position: relative;
  z-index: 1;
}
.hero-info h1 {
  margin-top: 0;
  font-size: 2.5rem;
}
.hero-info .year {
  color: #4ade80;
  font-weight: bold;
  font-size: 1.2rem;
}
.overview {
  line-height: 1.6;
  opacity: 0.8;
}
.item-actions h2 {
  font-size: 1.5rem;
  font-weight: 700;
  letter-spacing: -0.01em;
  margin-bottom: 1rem;
}
.collection-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.episode-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 0.75rem;
}
.episode-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.ep-info {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex: 1 1 auto;
}
.season-block {
  margin-bottom: 2.5rem;
}
.collection-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.2rem 0;
}
.ep-collections {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}
.btn-sm {
  padding: 4px 10px;
  font-size: 0.8rem;
  margin-left: 0.5rem;
}
.danger {
  background: rgba(255, 180, 171, 0.15) !important;
  border-color: rgba(255, 180, 171, 0.35) !important;
  color: #ffb4ab !important;
}
.danger:hover {
  background: rgba(255, 180, 171, 0.30) !important;
}
.meta-badge {
  background: rgba(255,255,255,0.1);
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  margin-left: 1rem;
}
.icon-only {
  padding: 4px 6px;
}
.poster-option:hover {
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}

.tmdb-link:hover {
  background: rgba(13, 37, 63, 0.9) !important;
  border-color: rgba(1, 180, 228, 0.8) !important;
  box-shadow: 0 0 10px rgba(1, 180, 228, 0.3);
  transform: translateY(-1px);
}

@media (max-width: 768px) {
  .item-detail-page {
    padding: 1rem;
    overflow-x: hidden;
  }
  .item-hero {
    flex-direction: column;
  }
  .hero-poster {
    width: 100%;
    max-width: 200px;
    margin: 0 auto;
  }
  .episode-list {
    grid-template-columns: 1fr;
  }
  .collection-item {
    flex-direction: column;
    align-items: stretch;
    gap: 0.75rem;
  }
  .col-actions,
  .collection-item > div:last-child {
    width: 100%;
    justify-content: flex-start !important;
  }
}
</style>
