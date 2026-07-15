FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ./src/*.csproj ./
RUN dotnet restore

COPY ./src/ ./
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends p7zip-full ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Bot.dll"]
