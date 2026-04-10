FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY src/ZoneAnalyzer.sln ./
COPY src/GeriRemenyi.Oanda.V20.Client/GeriRemenyi.Oanda.V20.Client.csproj GeriRemenyi.Oanda.V20.Client/
COPY src/GeriRemenyi.Oanda.V20.Sdk/GeriRemenyi.Oanda.V20.Sdk.csproj GeriRemenyi.Oanda.V20.Sdk/
COPY src/ZoneAnalyzer.PatternAnalysis/ZoneAnalyzer.PatternAnalysis.csproj ZoneAnalyzer.PatternAnalysis/
COPY src/ZoneAnalyzer.DataProvider/ZoneAnalyzer.DataProvider.csproj ZoneAnalyzer.DataProvider/
COPY src/ForexZoneAnalyzer.McpServer/ForexZoneAnalyzer.McpServer.csproj ForexZoneAnalyzer.McpServer/
# Stub out test projects so sln restore doesn't fail
COPY src/GeriRemenyi.Oanda.V20.Client.Test/GeriRemenyi.Oanda.V20.Client.Test.csproj GeriRemenyi.Oanda.V20.Client.Test/
COPY src/ZoneAnalyzer.PatternAnalysis.Test/ZoneAnalyzer.PatternAnalysis.Test.csproj ZoneAnalyzer.PatternAnalysis.Test/
COPY src/GeriRemenyi.Oanda.V20.Sdk.Playground/GeriRemenyi.Oanda.V20.Sdk.Playground.csproj GeriRemenyi.Oanda.V20.Sdk.Playground/
COPY src/ForexZoneAnalyzer.McpServer.Test/ForexZoneAnalyzer.McpServer.Test.csproj ForexZoneAnalyzer.McpServer.Test/

RUN dotnet restore ForexZoneAnalyzer.McpServer/ForexZoneAnalyzer.McpServer.csproj

# Copy all source and publish
COPY src/ .
RUN dotnet publish ForexZoneAnalyzer.McpServer/ForexZoneAnalyzer.McpServer.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ForexZoneAnalyzer.McpServer.dll"]
