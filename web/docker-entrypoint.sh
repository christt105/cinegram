#!/bin/sh
# Regenerate the SPA runtime config from the container environment.
# Runs via nginx's /docker-entrypoint.d hook before nginx starts, so a single
# prebuilt image can be pointed at any backend/bot-net ports or Jellyfin server.
set -e

cat > /usr/share/nginx/html/config.js <<EOF
window.__CINEGRAM__ = {
  JELLYFIN_URL: "${JELLYFIN_URL:-}",
  JELLYFIN_TOKEN: "${JELLYFIN_TOKEN:-}",
  BACKEND_URL: "${BACKEND_URL:-}",
  BACKEND_PORT: "${BACKEND_PORT:-}",
  BOT_NET_URL: "${BOT_NET_URL:-}",
  BOT_NET_PORT: "${BOT_NET_PORT:-}"
};
EOF
