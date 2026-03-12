FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/HashChallenge.Domain/HashChallenge.Domain.csproj src/HashChallenge.Domain/
COPY src/HashChallenge.Infrastructure/HashChallenge.Infrastructure.csproj src/HashChallenge.Infrastructure/
COPY src/HashChallenge.Api/HashChallenge.Api.csproj src/HashChallenge.Api/

# Restore
RUN dotnet restore src/HashChallenge.Api/HashChallenge.Api.csproj

# Copy source
COPY src/HashChallenge.Domain/ src/HashChallenge.Domain/
COPY src/HashChallenge.Infrastructure/ src/HashChallenge.Infrastructure/
COPY src/HashChallenge.Api/ src/HashChallenge.Api/

# Build and publish
RUN dotnet publish src/HashChallenge.Api/HashChallenge.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "HashChallenge.Api.dll"]
