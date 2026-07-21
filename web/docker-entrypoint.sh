#!/bin/sh
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
