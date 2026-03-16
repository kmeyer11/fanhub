FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/FixturesService/FixturesService.csproj", "src/Services/FixturesService/"]
RUN dotnet restore "src/Services/FixturesService/FixturesService.csproj"
COPY . .
WORKDIR "/src/src/Services/FixturesService"
RUN dotnet build "FixturesService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FixturesService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FixturesService.dll"]