FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src
COPY ["Server/Server.csproj", "Server/"]
COPY ["FgoData/FgoData.csproj", "FgoData/"]
COPY ["Rayshift/Rayshift.csproj", "Rayshift/"]
RUN dotnet restore "Server/Server.csproj"
COPY . .
WORKDIR "/src/Server"
RUN dotnet build "Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Server.dll"]
