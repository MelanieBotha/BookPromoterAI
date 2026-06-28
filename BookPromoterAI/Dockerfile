FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BookPromoterAI.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/data/bookpromoter.db
ENV UPLOADS_PATH=/data/uploads
RUN mkdir -p /data/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookPromoterAI.dll"]
