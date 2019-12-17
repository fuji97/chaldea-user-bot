FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

# Copy everything else and build
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app
COPY --from=build-env /app/Server/out .
# ENTRYPOINT ["dotnet", "Server.dll"]                   # Local development
CMD ASPNETCORE_URLS=http://*:$PORT dotnet Server.dll    # Heroku deploy
