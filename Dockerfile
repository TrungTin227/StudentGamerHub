# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and all project .csproj files (maintain folder structure)
COPY ["StudentGamerHub.sln", "./"]
COPY ["WebAPI/WebAPI.csproj", "WebAPI/"]
COPY ["BusinessObjects/BusinessObjects.csproj", "BusinessObjects/"]
COPY ["DTOs/DTOs.csproj", "DTOs/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Services/Services.csproj", "Services/"]

# Restore all dependencies for the solution
RUN dotnet restore "StudentGamerHub.sln"

# Copy everything else and publish the WebAPI project
COPY . .
WORKDIR /src/WebAPI
RUN dotnet publish "WebAPI.csproj" -c Release -o /app/publish

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS="http://+:80"

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WebAPI.dll"]
