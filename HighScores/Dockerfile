FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HighScores/HighScores.csproj", "HighScores/"]
RUN dotnet restore "HighScores/HighScores.csproj"
COPY . .
WORKDIR "/src/HighScores"
RUN dotnet build "HighScores.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HighScores.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HighScores.dll"]
