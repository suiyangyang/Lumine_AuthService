FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && ln -sf /usr/bin/python3 /usr/bin/python \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet workload install wasm-tools

# Restore inputs first so source changes do not invalidate the expensive package restore layer.
COPY ["Lumine_BuildingBlocks/Lumine.SeedWork/Lumine.SeedWork.csproj", "Lumine_BuildingBlocks/Lumine.SeedWork/"]
COPY ["Lumine_BuildingBlocks/Lumine.Services.Core/Lumine.Services.Core.csproj", "Lumine_BuildingBlocks/Lumine.Services.Core/"]
COPY ["Lumine_AuthService/Lumine.AuthPortal/Directory.Packages.props", "Lumine_AuthService/Lumine.AuthPortal/"]
COPY ["Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal/Lumine.AuthPortal.csproj", "Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal/"]
COPY ["Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj", "Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/"]
COPY ["Lumine_AuthService/Lumine.AuthServer/Lumine.AuthServer.csproj", "Lumine_AuthService/Lumine.AuthServer/"]

COPY . .

RUN dotnet build "Lumine_BuildingBlocks/Lumine.SeedWork/Lumine.SeedWork.csproj" -c $BUILD_CONFIGURATION \
    && dotnet pack "Lumine_BuildingBlocks/Lumine.SeedWork/Lumine.SeedWork.csproj" -c $BUILD_CONFIGURATION --no-build -o /tmp/local-packages \
    && dotnet build "Lumine_BuildingBlocks/Lumine.Services.Core/Lumine.Services.Core.csproj" -c $BUILD_CONFIGURATION \
    && dotnet pack "Lumine_BuildingBlocks/Lumine.Services.Core/Lumine.Services.Core.csproj" -c $BUILD_CONFIGURATION --no-build -o /tmp/local-packages

RUN dotnet restore "Lumine_AuthService/Lumine.AuthServer/Lumine.AuthServer.csproj" \
        --source /tmp/local-packages \
        --source https://api.nuget.org/v3/index.json \
    && dotnet restore "Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj" \
        --source https://api.nuget.org/v3/index.json

# Browser is the only producer of the final frontend site.
RUN rm -rf /app/browser-publish-output /app/browser-publish /app/publish \
    && dotnet publish "Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/browser-publish-output \
        --no-restore \
    && mkdir -p /app/browser-publish \
    && if [ -f /app/browser-publish-output/wwwroot/index.html ]; then \
        cp -r /app/browser-publish-output/wwwroot/. /app/browser-publish/; \
    else \
        cp -r /app/browser-publish-output/. /app/browser-publish/; \
    fi \
    && test -f /app/browser-publish/index.html \
    && test -f /app/browser-publish/_framework/dotnet.js \
    && find /app/browser-publish/_framework -type f -name '*.wasm' -print -quit | grep -q .

# Server publish must not trigger another Browser build or assemble static files.
RUN dotnet publish "Lumine_AuthService/Lumine.AuthServer/Lumine.AuthServer.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        /p:UseAppHost=false \
        /p:SkipBrowserBuild=true \
        --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG APP_UID=64198
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
RUN rm -rf ./wwwroot
COPY --from=build /app/browser-publish/. ./wwwroot/

RUN test -f ./wwwroot/index.html \
    && test -f ./wwwroot/_framework/dotnet.js \
    && find ./wwwroot/_framework -type f -name '*.wasm' -print -quit | grep -q .

USER $APP_UID
ENTRYPOINT ["dotnet", "Lumine.AuthServer.dll"]
