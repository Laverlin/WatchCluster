FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["IB.WatchCluster.YasTelegramBot/IB.WatchCluster.YasTelegramBot.csproj", "IB.WatchCluster.YasTelegramBot/"]
RUN dotnet restore "IB.WatchCluster.YasTelegramBot/IB.WatchCluster.YasTelegramBot.csproj"
COPY . .
WORKDIR "/src/IB.WatchCluster.YasTelegramBot"
RUN dotnet build "IB.WatchCluster.YasTelegramBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IB.WatchCluster.YasTelegramBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IB.WatchCluster.YasTelegramBot.dll"]
