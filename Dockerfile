# One image: the React storefront is built and served by the .NET API from wwwroot.

FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/src/Plushies.Api/Plushies.Api.csproj backend/src/Plushies.Api/
RUN dotnet restore backend/src/Plushies.Api/Plushies.Api.csproj
COPY backend/src/Plushies.Api/ backend/src/Plushies.Api/
RUN dotnet publish backend/src/Plushies.Api/Plushies.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
# Uploaded product photos live here; mount a volume so they survive redeploys.
RUN mkdir -p /app/uploads && chown -R $APP_UID /app/uploads
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Plushies.Api.dll"]
