FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/AgentForge.Api/AgentForge.Api.csproj src/AgentForge.Api/
COPY src/AgentForge.Application/AgentForge.Application.csproj src/AgentForge.Application/
COPY src/AgentForge.Domain/AgentForge.Domain.csproj src/AgentForge.Domain/
COPY src/AgentForge.Infrastructure/AgentForge.Infrastructure.csproj src/AgentForge.Infrastructure/
RUN dotnet restore src/AgentForge.Api/AgentForge.Api.csproj
COPY src/ src/
RUN dotnet publish src/AgentForge.Api/AgentForge.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "AgentForge.Api.dll"]
