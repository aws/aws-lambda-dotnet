# Based on Docker image from: https://github.com/dotnet/dotnet-docker/

ARG ASPNET_VERSION=5.0.7
ARG ASPNET_SHA512=fc18b588eebac0a40cac2abbbd2755b2e96e3c799fd6f51f10da096e3048f060d1473fe9fefe5269142d697bd01db65c55fb67cc823f8d4461ad195f0691a557
ARG ICU_MD5=6a99b541ea01f271257b121a4433c7c0

#TODO: remove before launch
ARG AMAZON_LINUX=265565150187.dkr.ecr.us-west-2.amazonaws.com/awslambda/provided.al2-runtime:base-image-arm64

# TODO: replace with public image before launch
#ARG AMAZON_LINUX=public.ecr.aws/lambda/provided:al2

FROM $AMAZON_LINUX AS base

FROM base AS builder-libicu
ARG ICU_MD5

WORKDIR /

# Install depedencies to extract and build ICU library
RUN yum install -d1 -y \
    tar \
    gzip \
    make \
    gcc-c++

# Download, validate and extract ICU library
# https://github.com/unicode-org/icu/releases/tag/release-68-1
RUN curl -SL https://github.com/unicode-org/icu/releases/download/release-68-1/icu4c-68_1-src.tgz -o icu-src.tgz \
    && echo "$ICU_MD5  icu-src.tgz" | md5sum -c - \
    && tar -xzf icu-src.tgz \
    && rm icu-src.tgz

# Build ICU library
RUN mkdir ~/libicu
WORKDIR /icu/source/
RUN ./configure --prefix=/libicu \
    && make \
    && make install

FROM base AS builder-net5
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


FROM base as final
ARG ASPNET_VERSION

ENV DOTNET_VERSION $ASPNET_VERSION

# Copy native dependencies
COPY --from=builder-libicu /libicu /usr/share/libicu

# Setup path
ENV LD_LIBRARY_PATH="/usr/share/libicu/lib:${LD_LIBRARY_PATH}"

COPY --from=builder-net5 ["/dotnet", "/var/lang/bin"]

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS builder
WORKDIR /src
COPY ["Libraries/src/Amazon.Lambda.RuntimeSupport", "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/"]
COPY ["Libraries/src/Amazon.Lambda.Core", "Repo/Libraries/src/Amazon.Lambda.Core/"]
COPY ["buildtools/", "Repo/buildtools/"]
RUN dotnet restore "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/Amazon.Lambda.RuntimeSupport.csproj"
WORKDIR "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport"
RUN dotnet build "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false --runtime linux-arm64 -c Release -o /app/build

FROM builder AS publish
RUN dotnet publish "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false --runtime linux-arm64 --self-contained false -p:PublishReadyToRun=true -c Release -o /app/publish
RUN apt-get update && apt-get install -y dos2unix
RUN dos2unix /app/publish/lambda-entrypoint.sh

FROM final
WORKDIR /var/task

ENV \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Lambda is opinionated about installing tooling under /var
    DOTNET_ROOT=/var/lang/bin \
    # Don't display welcome message on first run
    DOTNET_NOLOGO=true \
    # Disable Microsoft's telemetry collection
    DOTNET_CLI_TELEMETRY_OPTOUT=true

COPY --from=publish /app/publish /var/runtime

COPY --from=publish /app/publish/lambda-entrypoint.sh /
RUN rm -f /var/runtime/lambda-entrypoint.sh && \
    chmod +x /lambda-entrypoint.sh

ENTRYPOINT ["/lambda-entrypoint.sh"]
