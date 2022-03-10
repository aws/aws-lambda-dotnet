# Based on Docker image from: https://github.com/dotnet/dotnet-docker/

ARG ASPNET_VERSION=6.0.3
ARG ASPNET_SHA512=745586b64d3e01f856c366821f6fb8ca97c55b2a90ba36d528fdf99c98938574805153e7d4fff0560afe8382bea14b35ddeba391a2dc2328285f02e125c9b702

ARG ICU_VERSION=68.1
ARG ICU_MD5=6a99b541ea01f271257b121a4433c7c0

ARG LAMBDA_RUNTIME_NAME=dotnet6
ARG AMAZON_LINUX=public.ecr.aws/lambda/provided:al2

FROM $AMAZON_LINUX AS base

FROM base AS builder-libicu
WORKDIR /

# Install depedencies to extract and build ICU library
RUN yum install -d1 -y \
    tar \
    gzip \
    make \
    gcc-c++

# Download, validate and extract ICU library
# https://github.com/unicode-org/icu/releases/tag/release-68-1
ARG ICU_VERSION
ARG ICU_MD5
RUN curl -SL https://github.com/unicode-org/icu/releases/download/release-${ICU_VERSION//./-}/icu4c-${ICU_VERSION//./_}-src.tgz -o icu-src.tgz \
    && echo "$ICU_MD5  icu-src.tgz" | md5sum -c - \
    && tar -xzf icu-src.tgz \
    && rm icu-src.tgz

# Build ICU library
RUN mkdir /libicu
WORKDIR /icu/source/
RUN ./configure --prefix=/libicu \
    && make \
    && make install


FROM base AS builder-net6
ARG ASPNET_VERSION
ARG ASPNET_SHA512

WORKDIR /dotnet

# Install tar and gzip for unarchiving downloaded tar.gz
RUN yum install tar gzip --assumeyes

# Install the ASP.NET Core shared framework
RUN curl -SL --output aspnetcore.tar.gz https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-linux-arm64.tar.gz \
    && aspnetcore_sha512=$ASPNET_SHA512 \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -ozxf aspnetcore.tar.gz -C /dotnet \
    && rm aspnetcore.tar.gz


FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS builder
WORKDIR /src
COPY ["Libraries/src/Amazon.Lambda.RuntimeSupport", "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/"]
COPY ["Libraries/src/Amazon.Lambda.Core", "Repo/Libraries/src/Amazon.Lambda.Core/"]
COPY ["buildtools/", "Repo/buildtools/"]
RUN dotnet restore "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/Amazon.Lambda.RuntimeSupport.csproj" /p:TargetFrameworks=net6.0
WORKDIR "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport"
RUN dotnet build "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net6.0 --runtime linux-arm64 -c Release -o /app/build


FROM builder AS publish
RUN dotnet publish "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net6.0 -f net6.0 --runtime linux-arm64 --self-contained false -p:PublishReadyToRun=true -c Release -o /app/publish
RUN apt-get update && apt-get install -y dos2unix
RUN dos2unix /app/publish/bootstrap.sh && \
    mv /app/publish/bootstrap.sh /app/publish/bootstrap && \
    chmod +x /app/publish/bootstrap

# Copy native dependencies
COPY --from=builder-libicu  /libicu         /app/publish

# Generate runtime-release file
ARG LAMBDA_RUNTIME_NAME
RUN export BUILD_TIMESTAMP=$(printf '%x' $(date +%s)) && \
    export LOGGING_PROTOCOL="LOGGING=amzn-stdout-tlv" && \
    export LAMBDA_RUNTIME_NAME="LAMBDA_RUNTIME_NAME=${LAMBDA_RUNTIME_NAME}" && \
    echo "NAME=dotnet\nVERSION=${ASPNET_VERSION}-${BUILD_TIMESTAMP}\n${LOGGING_PROTOCOL}\n${LAMBDA_RUNTIME_NAME}\n" > /app/publish/runtime-release


FROM base

ARG ASPNET_VERSION

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

COPY --from=builder-net6    /dotnet         ${DOTNET_ROOT}
COPY --from=publish         /app/publish    ${LAMBDA_RUNTIME_DIR}

# Entrypoint is inherited from public.ecr.aws/lambda/provided
