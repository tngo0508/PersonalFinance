# Multi-stage Dockerfile for PersonalFinance.Web (.NET 10)
# Uses Ubuntu Noble chiseled runtime image for minimal attack surface & distroless non-root security.
# Optimized for fast cold starts on Azure Container Apps (ACA) Consumption tier.

ARG DOTNET_VERSION=10.0
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
# Node for the Release build's JS/CSS minification. Pinned LTS; Debian bookworm glibc runs on the noble SDK image.
ARG NODE_IMAGE=node:22.21.1-bookworm-slim

# ---------------------------------------------------------
# Stage 0: Node toolchain (copied into the SDK build stage only, never into the runtime image)
# ---------------------------------------------------------
FROM ${NODE_IMAGE} AS node

# ---------------------------------------------------------
# Stage 1: Base Runtime Image (Distroless / Chiseled Non-Root)
# ---------------------------------------------------------
FROM ${RUNTIME_IMAGE} AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Enable Tiered Compilation for faster first-request JIT
    DOTNET_TieredCompilation=1 \
    # Profile-Guided Optimization for faster Tiered Compilation
    DOTNET_TieredPGO=1
USER $APP_UID

# ---------------------------------------------------------
# Stage 2: Build & Restore Dependencies
# ---------------------------------------------------------
FROM ${SDK_IMAGE} AS build
ARG BUILD_CONFIGURATION=Release
ARG RUNTIME_IDENTIFIER=linux-x64

# Node + npm from the official Node image, so the Release publish can minify the Web assets.
# The final chiseled runtime stage copies only /app/publish, so it stays Node-free.
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
    && node --version \
    && npm --version

WORKDIR /src

# Copy the shared MSBuild files: Directory.Build.props (TargetFramework, Nullable, analyzers) and
# Directory.Packages.props (Central Package Management). Without them restore fails with NETSDK1013.
COPY ["PersonalFinance/Directory.Build.props", "PersonalFinance/Directory.Packages.props", "PersonalFinance/"]

# Copy project files for granular restore layer caching
COPY ["PersonalFinance/src/PersonalFinance.Shared/PersonalFinance.Shared.csproj", "PersonalFinance/src/PersonalFinance.Shared/"]
COPY ["PersonalFinance/src/PersonalFinance.Data/PersonalFinance.Data.csproj", "PersonalFinance/src/PersonalFinance.Data/"]
COPY ["PersonalFinance/src/PersonalFinance.ServiceDefaults/PersonalFinance.ServiceDefaults.csproj", "PersonalFinance/src/PersonalFinance.ServiceDefaults/"]
COPY ["PersonalFinance/src/PersonalFinance.Web/PersonalFinance.Web.csproj", "PersonalFinance/src/PersonalFinance.Web/"]

# Restore with RuntimeIdentifier and PublishReadyToRun so the crossgen2 runtime packages are available
RUN dotnet restore "PersonalFinance/src/PersonalFinance.Web/PersonalFinance.Web.csproj" \
    -r $RUNTIME_IDENTIFIER \
    /p:PublishReadyToRun=true

# Copy source code
COPY ["PersonalFinance/src/PersonalFinance.Shared/", "PersonalFinance/src/PersonalFinance.Shared/"]
COPY ["PersonalFinance/src/PersonalFinance.Data/", "PersonalFinance/src/PersonalFinance.Data/"]
COPY ["PersonalFinance/src/PersonalFinance.ServiceDefaults/", "PersonalFinance/src/PersonalFinance.ServiceDefaults/"]
COPY ["PersonalFinance/src/PersonalFinance.Web/", "PersonalFinance/src/PersonalFinance.Web/"]

WORKDIR "/src/PersonalFinance/src/PersonalFinance.Web"

# ---------------------------------------------------------
# Stage 3: Publish Standalone / Release Artifacts
# ---------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG RUNTIME_IDENTIFIER=linux-x64
# CI passes AppVersion.Current; 1.0.0 is the SDK default a build without it already gets
ARG APP_VERSION=1.0.0
# publish compiles the project itself, so there is no separate `dotnet build` step (saves a full compile per image)
# ReadyToRun (R2R) pre-compiles IL to native code, reducing cold-start JIT by ~30-50%
# NOTE: Trimming is DISABLED because EF Core uses reflection-heavy patterns (Include, ThenInclude,
# migrations) that are incompatible with aggressive trimming at TrimMode=link.
RUN dotnet publish "PersonalFinance.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -r $RUNTIME_IDENTIFIER \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true \
    /p:PublishReadyToRunComposite=true \
    /p:IlcOptimization=speed \
    /p:Version=$APP_VERSION

# ---------------------------------------------------------
# Stage 4: Production Final Runtime
# ---------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# NOTE: HEALTHCHECK is omitted because chiseled images have no shell (sh/bash).
# ACA uses its own probe configuration (liveness/readiness/startup) defined in Bicep.
ENTRYPOINT ["dotnet", "PersonalFinance.Web.dll"]
