FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BookPromoterAI.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Match root Dockerfile: jammy + ffmpeg/espeak for TikTok video rendering
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       espeak-ng \
       libespeak-ng1 \
       libttspico-utils \
       ffmpeg \
       fonts-dejavu-core \
       fontconfig \
    && test -x /usr/bin/espeak-ng \
    && test -x /usr/bin/ffmpeg \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/data/bookpromoter.db
ENV UPLOADS_PATH=/data/uploads
RUN mkdir -p /data/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookPromoterAI.dll"]
