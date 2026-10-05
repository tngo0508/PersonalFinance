# Multi-stage Dockerfile for PersonalFinance.Web (.NET 10)
# Uses Ubuntu Noble chiseled runtime image for minimal attack surface & distroless non-root security.

ARG DOTNET_VERSION=10.0
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra

# ---------------------------------------------------------
# Stage 1: Base Runtime Image (Distroless / Chiseled Non-Root)
# ---------------------------------------------------------
FROM ${RUNTIME_IMAGE} AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
USER $APP_UID

# ---------------------------------------------------------
# Stage 2: Build & Restore Dependencies
# ---------------------------------------------------------
FROM ${SDK_IMAGE} AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy Directory.Packages.props for Central Package Management (CPM) caching
COPY ["PersonalFinance/Directory.Packages.props", "PersonalFinance/"]

# Copy project files for granular restore layer caching
COPY ["PersonalFinance/src/PersonalFinance.Shared/PersonalFinance.Shared.csproj", "PersonalFinance/src/PersonalFinance.Shared/"]
COPY ["PersonalFinance/src/PersonalFinance.Data/PersonalFinance.Data.csproj", "PersonalFinance/src/PersonalFinance.Data/"]
COPY ["PersonalFinance/src/PersonalFinance.ServiceDefaults/PersonalFinance.ServiceDefaults.csproj", "PersonalFinance/src/PersonalFinance.ServiceDefaults/"]
COPY ["PersonalFinance/src/PersonalFinance.Web/PersonalFinance.Web.csproj", "PersonalFinance/src/PersonalFinance.Web/"]

RUN dotnet restore "PersonalFinance/src/PersonalFinance.Web/PersonalFinance.Web.csproj"

# Copy source code
COPY ["PersonalFinance/src/PersonalFinance.Shared/", "PersonalFinance/src/PersonalFinance.Shared/"]
COPY ["PersonalFinance/src/PersonalFinance.Data/", "PersonalFinance/src/PersonalFinance.Data/"]
COPY ["PersonalFinance/src/PersonalFinance.ServiceDefaults/", "PersonalFinance/src/PersonalFinance.ServiceDefaults/"]
COPY ["PersonalFinance/src/PersonalFinance.Web/", "PersonalFinance/src/PersonalFinance.Web/"]

WORKDIR "/src/PersonalFinance/src/PersonalFinance.Web"
RUN dotnet build "PersonalFinance.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ---------------------------------------------------------
# Stage 3: Publish Standalone / Release Artifacts
# ---------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PersonalFinance.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ---------------------------------------------------------
# Stage 4: Production Final Runtime
# ---------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PersonalFinance.Web.dll"]
