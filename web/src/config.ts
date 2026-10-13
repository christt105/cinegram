// The API, including the Jellyfin proxy, is served from the web's own origin under /api
// (the Vite dev server proxies it to a running Cinegram).

export const apiUrl = '/api';

export const jellyfinProxyUrl = `${apiUrl}/jellyfin`;

export const appVersion =
  (import.meta.env.VITE_APP_VERSION as string | undefined)?.trim() || 'dev';
