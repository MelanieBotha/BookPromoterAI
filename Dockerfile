FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BookPromoterAI/BookPromoterAI.csproj BookPromoterAI/
RUN dotnet restore BookPromoterAI/BookPromoterAI.csproj
COPY BookPromoterAI/ BookPromoterAI/
WORKDIR /src/BookPromoterAI
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy
ARG APP_RELEASE=1.11.5
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       espeak-ng \
       libespeak-ng1 \
       libttspico-utils \
       ffmpeg \
       fonts-dejavu-core \
       fontconfig \
    && espeak-ng --version \
    && ffmpeg -version \
    && test -x /usr/bin/espeak-ng \
    && test -x /usr/bin/ffmpeg \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/data/bookpromoter.db
RUN mkdir -p /data wwwroot/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookPromoterAI.dll"]
