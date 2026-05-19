# Multi-stage build: NativeAOT must be compiled on the target Linux toolchain.
# Build context is staged by the integration test (DurableFunctionDeployment.cs)
# and contains: buildtools/, Libraries/src/, and this function dir under
# Libraries/test/Amazon.Lambda.DurableExecution.IntegrationTests/TestFunctions/WaitOnlyAotFunction/
# so the project's relative ProjectReferences (..\..\..\..\src\...) resolve.

FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS build

RUN dnf install -y \
        clang \
        zlib-devel \
        krb5-libs \
        openssl-libs \
        tar \
        gzip \
        libicu \
    && dnf clean all

# Install the .NET 8, 9, and 10 SDKs. dotnet-install.sh works on AL2023 where
# `dnf install dotnet-sdk-*` may not be available.
# All three SDKs are required: the AOT project targets net8.0, but its ProjectReferences
# target net9.0 (RuntimeSupport, SnapshotRestore.Registry) and net10.0 (Core,
# DurableExecution, Serialization.SystemTextJson). Restore enumerates every TFM of
# every referenced project, so all SDKs must be present even when only net8.0 builds.
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

WORKDIR /src
COPY . .

WORKDIR /src/Libraries/test/Amazon.Lambda.DurableExecution.IntegrationTests/TestFunctions/WaitOnlyAotFunction
RUN dotnet publish -c Release -r linux-x64 -o /publish

FROM public.ecr.aws/lambda/provided:al2023
RUN dnf install -y libicu && dnf clean all
COPY --from=build /publish/ ${LAMBDA_TASK_ROOT}
ENTRYPOINT ["/var/task/bootstrap"]
