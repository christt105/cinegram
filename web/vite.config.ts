import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  server: {
    proxy: {
      '/api/maintenance/create-media': {
        target: process.env.BOT_NET_URL ?? 'http://localhost:8088',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api/maintenance/': {
        target: process.env.BACKEND_URL ?? 'http://localhost:8005',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api': {
        target: process.env.BOT_NET_URL ?? 'http://localhost:8088',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/bot': {
        target: process.env.BOT_NET_URL ?? 'http://localhost:8088',
        rewrite: (path) => path.replace(/^\/bot/, ''),
      },
    },
  },
  plugins: [
    vue(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'favicon.png'],
      manifest: {
        name: 'Cinegram',
        short_name: 'Cinegram',
        description: 'Cinegram media dashboard',
        theme_color: '#131313',
        background_color: '#131313',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          {
            src: 'pwa-maskable-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
    }),
  ],
})
