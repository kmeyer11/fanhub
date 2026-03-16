FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/NotificationsService/NotificationsService.csproj", "src/Services/NotificationsService/"]
RUN dotnet restore "src/Services/NotificationsService/NotificationsService.csproj"
COPY . .
WORKDIR "/src/src/Services/NotificationsService"
RUN dotnet build "NotificationsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NotificationsService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotificationsService.dll"]