#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["IB.WatchCluster.DbMigration/IB.WatchCluster.DbMigration.csproj", "IB.WatchCluster.DbMigration/"]
COPY ["IB.WatchCluster.Abstract/IB.WatchCluster.Abstract.csproj", "IB.WatchCluster.Abstract/"]
COPY ["IB.WatchCluster.DbSink/IB.WatchCluster.DbSink.csproj", "IB.WatchCluster.DbSink/"]
RUN dotnet restore "IB.WatchCluster.DbMigration/IB.WatchCluster.DbMigration.csproj"
COPY . .
WORKDIR "/src/IB.WatchCluster.DbMigration"
RUN dotnet build "IB.WatchCluster.DbMigration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IB.WatchCluster.DbMigration.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IB.WatchCluster.DbMigration.dll"]