FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["StudentGamerHub.sln", "./"]
COPY ["WebAPI/WebAPI.csproj", "WebAPI/"]
COPY ["BusinessObjects/BusinessObjects.csproj", "BusinessObjects/"]
COPY ["DTOs/DTOs.csproj", "DTOs/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Services/Services.csproj", "Services/"]

RUN dotnet restore "StudentGamerHub.sln" --ignore-failed-sources || true


COPY . .

RUN rm -rf ./Tests || true


WORKDIR /src/WebAPI
RUN dotnet publish "WebAPI.csproj" -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS="http://+:80"

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WebAPI.dll"]
