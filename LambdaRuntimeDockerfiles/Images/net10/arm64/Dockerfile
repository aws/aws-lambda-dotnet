# Based on Docker image from: https://github.com/dotnet/dotnet-docker/

ARG ASPNET_VERSION=10.0.0-preview.6.25358.103
ARG ASPNET_SHA512=88667d66d90aaba30649f84d10d057da7c0db479cdb36b76824dc8e0f02a44a6bf5d269d74daa0d6b78376f2dc66b60238558267555e2ced40cbd06aac745d6a

ARG LAMBDA_RUNTIME_NAME=dotnet10
ARG AMAZON_LINUX=public.ecr.aws/lambda/provided:al2023

FROM $AMAZON_LINUX AS base

RUN dnf install libicu-67.1-7.amzn2023.0.3.aarch64 --assumeyes

FROM base AS builder-net10
ARG ASPNET_VERSION
ARG ASPNET_SHA512

WORKDIR /dotnet

# Install tar and gzip for unarchiving downloaded tar.gz
RUN dnf install tar gzip --assumeyes

# Install the ASP.NET Core shared framework
RUN curl -SL --output aspnetcore.tar.gz https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-linux-arm64.tar.gz \
    && aspnetcore_sha512=$ASPNET_SHA512 \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -ozxf aspnetcore.tar.gz -C /dotnet \
    && rm aspnetcore.tar.gz


FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-trixie-slim AS builder
WORKDIR /src
COPY ["Libraries/src/Amazon.Lambda.RuntimeSupport", "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/"]
COPY ["Libraries/src/SnapshotRestore.Registry", "Repo/Libraries/src/SnapshotRestore.Registry/"]
COPY ["Libraries/src/Amazon.Lambda.Core", "Repo/Libraries/src/Amazon.Lambda.Core/"]
COPY ["buildtools/", "Repo/buildtools/"]
RUN dotnet restore "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/Amazon.Lambda.RuntimeSupport.csproj" /p:TargetFrameworks=net10.0
WORKDIR "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport"
RUN dotnet build "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net10.0 --runtime linux-arm64 -c Release -o /app/build


FROM builder AS publish
RUN dotnet publish "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net10.0 -f net10.0 --runtime linux-arm64 --self-contained false -p:PublishReadyToRun=true -c Release -o /app/publish
RUN apt-get update && apt-get install -y dos2unix
RUN dos2unix /app/publish/bootstrap.sh && \
    mv /app/publish/bootstrap.sh /app/publish/bootstrap && \
    chmod +x /app/publish/bootstrap
RUN touch /app/publish/empty-certificates.crt


FROM base

ARG ASPNET_VERSION
ARG LAMBDA_RUNTIME_NAME

ENV \
    # Export .NET version as environment variable
    DOTNET_VERSION=$ASPNET_VERSION \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Lambda is opinionated about installing tooling under /var
    DOTNET_ROOT=/var/lang/bin \
    # Don't display welcome message on first run
    DOTNET_NOLOGO=true \
    # Disable Microsoft's telemetry collection
    DOTNET_CLI_TELEMETRY_OPTOUT=true

COPY --from=builder-net10    /dotnet         ${DOTNET_ROOT}
COPY --from=publish         /app/publish    ${LAMBDA_RUNTIME_DIR}

# Generate runtime-release file
RUN export BUILD_TIMESTAMP=$(printf '%x' $(date +%s)) && \
    export LOGGING_PROTOCOL="LOGGING=amzn-stdout-tlv" && \
    export LAMBDA_RUNTIME_NAME="LAMBDA_RUNTIME_NAME=${LAMBDA_RUNTIME_NAME}" && \
    echo -e "NAME=dotnet\nVERSION=${ASPNET_VERSION}-${BUILD_TIMESTAMP}\n${LOGGING_PROTOCOL}\n${LAMBDA_RUNTIME_NAME}\n" > ${LAMBDA_RUNTIME_DIR}/runtime-release

# Entrypoint is inherited from public.ecr.aws/lambda/provided
