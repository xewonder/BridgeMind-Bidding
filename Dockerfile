# BridgeMind bidding service — production image.
# Build context is the repo root; .dockerignore keeps the test/cli projects out of it.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore the API and its one project reference before copying sources, so the
# NuGet layer survives code-only changes.
COPY BridgeBidder/BridgeBidder.csproj BridgeBidder/
COPY BridgeMindBidding.Api/BridgeMindBidding.Api.csproj BridgeMindBidding.Api/
RUN dotnet restore BridgeMindBidding.Api/BridgeMindBidding.Api.csproj

COPY BridgeBidder/ BridgeBidder/
COPY BridgeMindBidding.Api/ BridgeMindBidding.Api/
RUN dotnet publish BridgeMindBidding.Api/BridgeMindBidding.Api.csproj \
      -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# PORT is read by the app at startup; it defaults to 8080 and ASPNETCORE_URLS still
# overrides it if the platform prefers to set explicit binding addresses.
ENV PORT=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BridgeMindBidding.Api.dll"]
