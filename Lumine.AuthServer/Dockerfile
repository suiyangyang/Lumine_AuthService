# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG APP_UID=64198
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN apt-get update \
	&& apt-get install -y --no-install-recommends python3 \
	&& ln -sf /usr/bin/python3 /usr/bin/python \
	&& rm -rf /var/lib/apt/lists/*

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
	--source https://api.nuget.org/v3/index.json

RUN dotnet workload install wasm-tools

RUN dotnet build "Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj" -c $BUILD_CONFIGURATION -v minimal \
	&& mkdir -p /app/browser-publish/_framework \
	&& cp -r Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/wwwroot/. /app/browser-publish/ \
	&& cp -r Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/bin/$BUILD_CONFIGURATION/net10.0-browser/wwwroot/_framework/. /app/browser-publish/_framework/ \
	&& if [ -d Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/bin/$BUILD_CONFIGURATION/net10.0-browser/wwwroot/_content ]; then mkdir -p /app/browser-publish/_content && cp -r Lumine_AuthService/Lumine.AuthPortal/Lumine.AuthPortal.Browser/bin/$BUILD_CONFIGURATION/net10.0-browser/wwwroot/_content/. /app/browser-publish/_content/; fi
WORKDIR "/src/Lumine_AuthService/Lumine.AuthServer"
RUN dotnet build "./Lumine.AuthServer.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Lumine.AuthServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /app/browser-publish/. ./wwwroot/
ENTRYPOINT ["dotnet", "Lumine.AuthServer.dll"]