#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["IB.WatchCluster.PgSink/IB.WatchCluster.PgSink.csproj", "IB.WatchCluster.PgSink/"]
COPY ["IB.WatchCluster.Abstract/IB.WatchCluster.Abstract.csproj", "IB.WatchCluster.Abstract/"]
RUN dotnet restore "IB.WatchCluster.PgSink/IB.WatchCluster.PgSink.csproj"
COPY . .
WORKDIR "/src/IB.WatchCluster.PgSink"
RUN dotnet build "IB.WatchCluster.PgSink.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IB.WatchCluster.PgSink.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IB.WatchCluster.PgSink.dll"]