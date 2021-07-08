# Based on Docker image from: https://github.com/dotnet/dotnet-docker/

ARG ASPNET_VERSION=6.0.0-rc.2.21480.10
ARG ASPNET_SHA512=d48895237644a3999663b2e16baa2303d8b77f66385a04d8edb5601fdffacce18b1b1318827d09f74f6b133ff2c179ef659bd21cf8460289b5f81f404fa8b326

ARG AMAZON_LINUX=public.ecr.aws/lambda/provided:al2

FROM $AMAZON_LINUX AS base

FROM base AS builder-net6
ARG ASPNET_VERSION
ARG ASPNET_SHA512

WORKDIR /dotnet

# Install tar and gzip for unarchiving downloaded tar.gz
RUN yum install tar gzip --assumeyes

# Install the ASP.NET Core shared framework
RUN curl -SL --output aspnetcore.tar.gz https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-linux-x64.tar.gz \
    && aspnetcore_sha512=$ASPNET_SHA512 \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -ozxf aspnetcore.tar.gz -C /dotnet \
    && rm aspnetcore.tar.gz


FROM base as final
ARG ASPNET_VERSION

ENV DOTNET_VERSION $ASPNET_VERSION

COPY --from=builder-net6 ["/dotnet", "/var/lang/bin"]

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS builder
WORKDIR /src
COPY ["Libraries/src/Amazon.Lambda.RuntimeSupport", "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/"]
COPY ["Libraries/src/Amazon.Lambda.Core", "Repo/Libraries/src/Amazon.Lambda.Core/"]
COPY ["buildtools/", "Repo/buildtools/"]
RUN dotnet restore "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport/Amazon.Lambda.RuntimeSupport.csproj" /p:TargetFrameworks=net6.0
WORKDIR "Repo/Libraries/src/Amazon.Lambda.RuntimeSupport"
RUN dotnet build "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net6.0 --runtime linux-x64 -c Release -o /app/build

FROM builder AS publish
RUN dotnet publish "Amazon.Lambda.RuntimeSupport.csproj" /p:ExecutableOutputType=true /p:GenerateDocumentationFile=false /p:TargetFrameworks=net6.0 -f net6.0 --runtime linux-x64 --self-contained false -p:PublishReadyToRun=true -c Release -o /app/publish
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