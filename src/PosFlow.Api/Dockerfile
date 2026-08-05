# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj files first and restore, so Docker can cache the
# (slow) NuGet restore layer separately from source changes.
COPY PosFlow.slnx .
COPY src/PosFlow.Domain/PosFlow.Domain.csproj src/PosFlow.Domain/
COPY src/PosFlow.Application/PosFlow.Application.csproj src/PosFlow.Application/
COPY src/PosFlow.Infrastructure/PosFlow.Infrastructure.csproj src/PosFlow.Infrastructure/
COPY src/PosFlow.Api/PosFlow.Api.csproj src/PosFlow.Api/
RUN dotnet restore src/PosFlow.Api/PosFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/PosFlow.Api/PosFlow.Api.csproj \
    -c Release \
    -o /app \
    --no-restore

# Runtime stage - much smaller image, no SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Runs as non-root by default in the aspnet image ("app" user, uid 64198)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PosFlow.Api.dll"]
