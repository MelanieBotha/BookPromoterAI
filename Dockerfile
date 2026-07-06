FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BookPromoterAI/BookPromoterAI.csproj BookPromoterAI/
RUN dotnet restore BookPromoterAI/BookPromoterAI.csproj
COPY BookPromoterAI/ BookPromoterAI/
WORKDIR /src/BookPromoterAI
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends espeak-ng && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/data/bookpromoter.db
RUN mkdir -p /data wwwroot/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookPromoterAI.dll"]
