FROM node:24-alpine AS frontend
WORKDIR /src/src/LabTrack.Api/ClientApp
COPY src/LabTrack.Api/ClientApp/package*.json ./
RUN npm ci
COPY src/LabTrack.Api/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/LabTrack.Api/LabTrack.Api.csproj src/LabTrack.Api/
RUN dotnet restore src/LabTrack.Api/LabTrack.Api.csproj
COPY src/LabTrack.Api/ src/LabTrack.Api/
COPY --from=frontend /src/src/LabTrack.Api/wwwroot/ src/LabTrack.Api/wwwroot/
RUN dotnet publish src/LabTrack.Api/LabTrack.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LabTrack.Api.dll"]
