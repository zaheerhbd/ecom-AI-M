FROM node:16-bullseye AS client-build
WORKDIR /src/client

COPY client/package.json client/package-lock.json* ./
RUN npm install --legacy-peer-deps

COPY client/. ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS api-build
WORKDIR /src

COPY eCom.sln ./
COPY Directory.Build.props ./
COPY global.json ./
COPY API/API.csproj API/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore API/API.csproj

COPY API/. API/
COPY Core/. Core/
COPY Infrastructure/. Infrastructure/
COPY --from=client-build /src/API/wwwroot ./API/wwwroot

RUN dotnet publish API/API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime
WORKDIR /app

COPY --from=api-build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API.dll"]
