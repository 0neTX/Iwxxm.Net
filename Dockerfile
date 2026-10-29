# syntax=docker/dockerfile:1

# ==============================================================================
# Stage 1: restore — Restore local .NET tools & NuGet packages (cached layer)
# The .NET 10 SDK builds every target: netstandard2.0/2.1 (Iwxxm.Net) and net10.0
# (Iwxxm.Net.Validation.Saxon, test projects, benchmarks).
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
ENV DOTNET_ROLL_FORWARD=Major
WORKDIR /workspace

# Copy solution (.slnx), global configuration, Central Package Management, and project manifests first for layer caching
COPY global.json Directory.Build.props Directory.Packages.props Iwxxm.Net.slnx ./
COPY .config/dotnet-tools.json ./.config/dotnet-tools.json
COPY src/Iwxxm.Net/Iwxxm.Net.csproj ./src/Iwxxm.Net/
COPY src/Iwxxm.Net.Validation.Saxon/Iwxxm.Net.Validation.Saxon.csproj ./src/Iwxxm.Net.Validation.Saxon/
COPY tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj ./tests/Iwxxm.Net.Tests/
COPY tests/Iwxxm.Net.Validation.Saxon.Tests/Iwxxm.Net.Validation.Saxon.Tests.csproj ./tests/Iwxxm.Net.Validation.Saxon.Tests/
COPY tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj ./tests/Iwxxm.Net.Benchmarks/

RUN dotnet tool restore && \
    dotnet restore Iwxxm.Net.slnx

# ==============================================================================
# Stage 2: build — Release Build (TreatWarningsAsErrors)
# Architecture boundaries are enforced at IL level by HexagonalArchitectureTests (test stage).
# ==============================================================================
FROM restore AS build
ARG PACKAGE_VERSION=1.0.0-rc.1
ARG FILE_VERSION=1.0.0.1

COPY . .

# Execute deterministic Release build with warnings as errors
RUN dotnet build Iwxxm.Net.slnx \
    --configuration Release \
    --no-restore \
    -p:Version=${PACKAGE_VERSION} \
    -p:FileVersion=${FILE_VERSION} \
    -p:ContinuousIntegrationBuild=true

# ==============================================================================
# Stage 3: test — Unit & Integration Tests (WMO IWXXM 2025-2 Suite + Coverage)
# ==============================================================================
FROM build AS test
RUN dotnet test tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj \
    --configuration Release \
    --no-build \
    --verbosity normal \
    --collect:"XPlat Code Coverage" \
    --results-directory /workspace/artifacts/test-results && \
    dotnet test tests/Iwxxm.Net.Validation.Saxon.Tests/Iwxxm.Net.Validation.Saxon.Tests.csproj \
    --configuration Release \
    --no-build \
    --verbosity normal \
    --collect:"XPlat Code Coverage" \
    --results-directory /workspace/artifacts/test-results

# ==============================================================================
# Stage 4: pack — Compile NuGet Package (.nupkg) & Symbol Package (.snupkg)
# ==============================================================================
FROM test AS pack
ARG PACKAGE_VERSION=1.0.0-rc.1
ARG FILE_VERSION=1.0.0.1

RUN for project in src/Iwxxm.Net/Iwxxm.Net.csproj src/Iwxxm.Net.Validation.Saxon/Iwxxm.Net.Validation.Saxon.csproj; do \
        dotnet pack "$project" \
            --configuration Release \
            --no-build \
            --output /workspace/artifacts \
            -p:Version=${PACKAGE_VERSION} \
            -p:FileVersion=${FILE_VERSION} \
            -p:ContinuousIntegrationBuild=true || exit 1; \
    done && \
    ls -la /workspace/artifacts/*.nupkg /workspace/artifacts/*.snupkg

# Guard: the core package must declare zero runtime dependencies in its nuspec
RUN perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
        'unzip $ARGV[0] => \my $nuspec, Name => "Iwxxm.Net.nuspec" or die "nuspec not found: $UnzipError\n"; \
         if ($nuspec =~ /<dependency\s/) { print STDERR "ERROR: Iwxxm.Net declares runtime dependencies\n"; exit 1 } \
         print "Iwxxm.Net nuspec: zero dependencies\n";' \
        /workspace/artifacts/Iwxxm.Net.${PACKAGE_VERSION}.nupkg

# ==============================================================================
# Stage 5: artifacts — Exportable scratch stage for host extraction
# Usage: docker build --target artifacts --output type=local,dest=./artifacts .
# ==============================================================================
FROM scratch AS artifacts
COPY --from=pack /workspace/artifacts/ /

# ==============================================================================
# Stage 6: ci — Default runtime stage (exports artifacts & optional GitHub push)
# Usage:
#   docker build -t iwxxm-net-ci .
#   docker run --rm -v "$(pwd)/artifacts:/out" iwxxm-net-ci
# ==============================================================================
FROM pack AS ci
ENV GITHUB_OWNER="0neTX"
ENV GITHUB_TOKEN=""

CMD ["/bin/bash", "-c", "\
    echo '====================================================================' && \
    echo ' Iwxxm.Net Local CI/CD Pipeline Completed Successfully' && \
    echo '====================================================================' && \
    ls -lh /workspace/artifacts/*.nupkg /workspace/artifacts/*.snupkg && \
    if [ -d /out ]; then \
        cp -rf /workspace/artifacts/* /out/ && \
        echo 'Copied compiled NuGet packages and test coverage reports to /out'; \
    fi && \
    if [ -n \"$GITHUB_TOKEN\" ]; then \
        echo \"Publishing package to GitHub Packages (owner: $GITHUB_OWNER)...\" && \
        dotnet nuget push \"/workspace/artifacts/*.nupkg\" \
            --source \"https://nuget.pkg.github.com/${GITHUB_OWNER}/index.json\" \
            --api-key \"$GITHUB_TOKEN\" \
            --skip-duplicate; \
    fi \
"]
