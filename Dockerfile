FROM node:20-slim AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
ARG APP_VERSION=dev
ENV VITE_APP_VERSION=$APP_VERSION
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY server/src/*.csproj ./
RUN dotnet restore

COPY server/src/ ./
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN sed -i 's/^Components: main$/Components: main non-free/' /etc/apt/sources.list.d/debian.sources \
    && apt-get update \
    && apt-get install -y --no-install-recommends p7zip-full p7zip-rar ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION
COPY --from=build /app ./
COPY --from=web /web/dist ./wwwroot
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Cinegram.dll"]
