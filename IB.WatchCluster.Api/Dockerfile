#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["IB.WatchCluster.Api/IB.WatchCluster.Api.csproj", "IB.WatchCluster.Api/"]
RUN dotnet restore "IB.WatchCluster.Api/IB.WatchCluster.Api.csproj"
COPY . .
WORKDIR "/src/IB.WatchCluster.Api"
RUN dotnet build "IB.WatchCluster.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IB.WatchCluster.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IB.WatchCluster.Api.dll"]