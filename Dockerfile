FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ReleasePilot.slnx .
COPY src/ReleasePilot.Domain/ReleasePilot.Domain.csproj src/ReleasePilot.Domain/
COPY src/ReleasePilot.Application/ReleasePilot.Application.csproj src/ReleasePilot.Application/
COPY src/ReleasePilot.Infrastructure/ReleasePilot.Infrastructure.csproj src/ReleasePilot.Infrastructure/
COPY src/ReleasePilot.Agent/ReleasePilot.Agent.csproj src/ReleasePilot.Agent/
COPY src/ReleasePilot.Api/ReleasePilot.Api.csproj src/ReleasePilot.Api/
COPY tests/ReleasePilot.Domain.Tests/ReleasePilot.Domain.Tests.csproj tests/ReleasePilot.Domain.Tests/
RUN dotnet restore

# Copy source and publish
COPY . .
WORKDIR /src/src/ReleasePilot.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "ReleasePilot.Api.dll"]
