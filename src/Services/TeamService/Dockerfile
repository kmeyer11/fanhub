FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/TeamService/TeamService.csproj", "src/Services/TeamService/"]
RUN dotnet restore "src/Services/TeamService/TeamService.csproj"
COPY . .
WORKDIR "/src/src/Services/TeamService"
RUN dotnet build "TeamService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TeamService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TeamService.dll"]