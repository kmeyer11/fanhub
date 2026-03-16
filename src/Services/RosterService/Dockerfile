FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/RosterService/RosterService.csproj", "src/Services/RosterService/"]
RUN dotnet restore "src/Services/RosterService/RosterService.csproj"
COPY . .
WORKDIR "/src/src/Services/RosterService"
RUN dotnet build "RosterService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RosterService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RosterService.dll"]