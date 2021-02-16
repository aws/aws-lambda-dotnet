# Based on Docker image from: https://github.com/dotnet/dotnet-docker/

ARG ASPNET_VERSION=5.0.3
ARG ASPNET_SHA512=188618b07cd5c97a34c77efccc7c4eba10a52681592ef711a005c82d243e601891418e0dcc27a22aae7dd6ae33d35e0bb7aa9b9fc022746c6c2414bd25cdb110
ARG AMAZON_LINUX=public.ecr.aws/lambda/provided:al2

FROM $AMAZON_LINUX AS base

FROM base AS builder-deps
COPY --from=base / /rootfs
RUN yum install -d1 -y --installroot=/rootfs \
    ca-certificates \
    \
    # .NET dependencies
    libc6 \
    libgcc1 \
    libgssapi-krb5-2 \
    libicu63 \
    libssl1.1 \
    libstdc++6 \
    zlib1g

FROM base
COPY --from=builder-deps /rootfs /

# Setup custom dotnet env variables
# See here for more info: https://github.com/dotnet/docs/blob/master/docs/core/tools/dotnet.md
ENV \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Lambda is optionated about installing tooling under /var
    DOTNET_ROOT=/var/lang/dotnet \
    # Don't display welcome message on first run
    DOTNET_NOLOGO=true \
    # Disable Microsoft's telemetry collection
    DOTNET_CLI_TELEMETRY_OPTOUT=true

FROM base AS builder-net5
ARG ASPNET_VERSION
ARG ASPNET_SHA512

WORKDIR /dotnet

# Install tar and gzip for unarchiving downloaded tar.gz
RUN yum install tar --assumeyes
RUN yum install gzip --assumeyes

# Install the ASP.NET Core shared framework
RUN curl -SL --output aspnetcore.tar.gz https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-linux-x64.tar.gz \
    && aspnetcore_sha512=$ASPNET_SHA512 \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -ozxf aspnetcore.tar.gz -C /dotnet \
    && rm aspnetcore.tar.gz


FROM base as final
ARG ASPNET_VERSION

ENV DOTNET_VERSION $ASPNET_VERSION

COPY --from=builder-net5 ["/dotnet", "/var/lang/bin"]

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS builder
WORKDIR /src
COPY ["Libraries/src/Amazon.Lambda.RuntimeSupport", "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/"]
COPY ["Libraries/src/Amazon.Lambda.Core", "Repo/Libraries/src/Amazon.Lambda.Core/"]
COPY ["buildtools/", "Repo/buildtools/"]
RUN dotnet restore "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/Amazon.Lambda.RuntimeSupport.csproj"
WORKDIR "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport"
RUN dotnet build "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false --runtime linux-x64 -c Release -o /app/build

FROM builder AS publish
RUN dotnet publish "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false --runtime linux-x64 --self-contained false -p:PublishReadyToRun=true -c Release -o /app/publish
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
