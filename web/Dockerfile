# Build the static site, then serve it with nginx.
FROM node:20-slim AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .

# Vite inlines VITE_* at build time, so deploy-time config arrives as build args.
ARG VITE_JELLYFIN_URL=
ARG VITE_JELLYFIN_TOKEN=
ARG VITE_BACKEND_PORT=8005
ARG VITE_BOT_NET_PORT=8088
ENV VITE_JELLYFIN_URL=$VITE_JELLYFIN_URL \
    VITE_JELLYFIN_TOKEN=$VITE_JELLYFIN_TOKEN \
    VITE_BACKEND_PORT=$VITE_BACKEND_PORT \
    VITE_BOT_NET_PORT=$VITE_BOT_NET_PORT
RUN npm run build

FROM nginx:alpine AS serve
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.sh /docker-entrypoint.d/40-cinegram-config.sh
RUN chmod +x /docker-entrypoint.d/40-cinegram-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://127.0.0.1/ || exit 1
