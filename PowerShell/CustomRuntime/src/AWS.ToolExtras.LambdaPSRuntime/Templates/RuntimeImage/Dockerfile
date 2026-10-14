# PowerShell runtime image for AWS Lambda (also the base of a container-per-function image)
# ---------------------------------------------------------------------------
# Based on the AWSLabs aws-lambda-powershell-runtime bootstrap, laid out as a runtime
# image: the bootstrap, runtime.ps1, pwsh and the helper modules under /var/runtime,
# rather than in a provided.al2023 layer (/opt).
#
# pwsh is obtained as follows: on amd64, the Microsoft GPG-signed RPM (provenance +
# dependency resolution); on arm64, where Microsoft publishes no aarch64 RPM, the
# official release tarball verified against a SHA256 pinned in the module. Both
# land in /opt/microsoft/powershell and a customer-managed runtime reserves /opt, so
# the interpreter is relocated into /var/runtime/powershell and /opt/microsoft is
# dropped. Because pwsh is installed in the image, its native deps (libicu,
# openssl-libs, krb5-libs, ...) are present, unlike the layer and self-contained .zip
# targets.
#
# Lambda requires a runtime image as a Docker v2 Schema 2 manifest, not OCI
# (built with --provenance=false + oci-mediatypes=false; see Build-LambdaPSRuntimeImage).
ARG PWSH_VERSION=__PWSH_VERSION__
ARG PWSH_ARM64_SHA256=__PWSH_ARM64_SHA256__

FROM public.ecr.aws/lambda/provided:al2023

ARG PWSH_VERSION
ARG PWSH_ARM64_SHA256
# Set by BuildKit from --platform (amd64 | arm64).
ARG TARGETARCH

# 1. Install pwsh and relocate it out of the reserved /opt path into /var/runtime. One RUN, so
#    the /opt copy is never kept in an image layer.
#    amd64: Microsoft's GPG-signed RPM repo; dnf resolves the system deps.
#    arm64: Microsoft publishes no aarch64 RPM in rhel/9/prod, so use the official
#    release tarball, verified against the SHA256 pinned in the module, and install
#    libicu (the base image already has openssl via openssl-snapsafe-libs, plus
#    krb5-libs, zlib and libstdc++). It is unpacked where the RPM would put it,
#    so the relocation is identical for both architectures.
RUN set -eu; \
    if [ "$TARGETARCH" = "arm64" ]; then \
        case "$PWSH_ARM64_SHA256" in \
            [0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f]*) ;; \
            *) echo "No pinned SHA256 for the pwsh ${PWSH_VERSION} arm64 tarball; cannot build linux/arm64." >&2; exit 1 ;; \
        esac; \
        dnf install -y tar gzip libicu; \
        curl -sSLf --retry __DOWNLOAD_RETRY_COUNT__ --retry-delay __DOWNLOAD_RETRY_DELAY__ --connect-timeout __DOWNLOAD_CONNECT_TIMEOUT__ --max-time __DOWNLOAD_OPERATION_TIMEOUT__ -o /tmp/pwsh.tar.gz \
            "__PWSH_RELEASE_URL__/v${PWSH_VERSION}/powershell-${PWSH_VERSION}-linux-arm64.tar.gz"; \
        echo "${PWSH_ARM64_SHA256}  /tmp/pwsh.tar.gz" | sha256sum -c -; \
        mkdir -p /opt/microsoft/powershell/7; \
        tar -xzf /tmp/pwsh.tar.gz -C /opt/microsoft/powershell/7; \
        rm -f /tmp/pwsh.tar.gz; \
    else \
        rpm --import https://packages.microsoft.com/keys/microsoft.asc; \
        curl -sSLf --retry __DOWNLOAD_RETRY_COUNT__ --retry-delay __DOWNLOAD_RETRY_DELAY__ --connect-timeout __DOWNLOAD_CONNECT_TIMEOUT__ --max-time __DOWNLOAD_OPERATION_TIMEOUT__ https://packages.microsoft.com/config/rhel/9/prod.repo \
            -o /etc/yum.repos.d/microsoft.repo; \
        dnf install -y "powershell-${PWSH_VERSION}"; \
    fi; \
    dnf clean all; \
    mkdir -p /var/runtime/powershell; \
    cp -a /opt/microsoft/powershell/7/. /var/runtime/powershell/; \
    rm -rf /opt/microsoft /usr/bin/pwsh; \
    chmod +x /var/runtime/powershell/pwsh; \
    /var/runtime/powershell/pwsh --version

# 2. PRECOMPILE the LambdaContext assembly at build time (cold-start optimisation).
#    Use the in-image pwsh's own Add-Type -OutputAssembly so the DLL targets the
#    exact same .NET runtime that will load it. This moves the ~1.7s Roslyn compile
#    off the cold-start path; runtime.ps1 loads this DLL with Add-Type -Path.
#    It comes before the files you edit, so a module change reuses it from the
#    build cache.
COPY PowerShellLambdaContext.cs /var/runtime/PowerShellLambdaContext.cs
RUN /var/runtime/powershell/pwsh -noprofile -c \
      "Add-Type -TypeDefinition ([IO.File]::ReadAllText('/var/runtime/PowerShellLambdaContext.cs')) -OutputAssembly '/var/runtime/PowerShellLambdaContext.dll'" \
 && test -f /var/runtime/PowerShellLambdaContext.dll \
 && echo 'LambdaContext.dll built OK'

# 3. Lay down the runtime: bootstrap, runtime.ps1 (the Runtime API event loop) and the helper
#    modules, under /var/runtime. Copying modules/ wholesale (incl. Private/) lets
#    pwsh-runtime.psm1 dot-source the private functions at load, so no build-time
#    merge is needed. Modules you add go in ./modules too, so they land in
#    /var/runtime/modules (on PSModulePath). The bootstrap is an sh wrapper that execs
#    pwsh on runtime.ps1; it is executed directly, so a leading UTF-8 byte order mark
#    and CRLF line endings (an edit on Windows) are stripped.
COPY --chmod=755 bootstrap     /var/runtime/bootstrap
COPY runtime.ps1               /var/runtime/runtime.ps1
COPY modules                   /var/runtime/modules
RUN sed -i -e '1s/^\xEF\xBB\xBF//' -e 's/\r$//' /var/runtime/bootstrap

# Published as a customer-managed runtime, the image's ENTRYPOINT, CMD and environment
# variables are ignored: /var/runtime/bootstrap is the contract.
