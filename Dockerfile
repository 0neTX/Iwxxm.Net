# syntax=docker/dockerfile:1

# ==============================================================================
# Stage 1: restore — Restore NuGet packages (cached layer)
# The .NET 10 SDK builds every target: netstandard2.0/2.1 (Iwxxm.Net), netstandard2.0
# (Iwxxm.Net.Export.GeoPackage) and net10.0 (Iwxxm.Net.Validation.Saxon, test projects, benchmarks).
# The SDK image is pinned (AUD-M18): tag 10.0.403 (SDK 10.0.403, runtime 10.0.14, within the
# global.json band 10.0.100 + latestFeature) and its digest, so a rebuild uses the same toolchain.
# No `dotnet tool restore`: the only local tool (xscgen) is a design-time helper (AUD-L11).
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS restore
WORKDIR /workspace

# Copy solution (.slnx), global configuration, package sources, Central Package Management and project manifests first
# for layer caching; nuget.config is copied before the restore so that its package sources apply (AUD-L11).
COPY global.json nuget.config Directory.Build.props Directory.Packages.props Iwxxm.Net.slnx ./
COPY src/Iwxxm.Net/Iwxxm.Net.csproj ./src/Iwxxm.Net/
COPY src/Iwxxm.Net.Validation.Saxon/Iwxxm.Net.Validation.Saxon.csproj ./src/Iwxxm.Net.Validation.Saxon/
COPY src/Iwxxm.Net.Export.GeoPackage/Iwxxm.Net.Export.GeoPackage.csproj ./src/Iwxxm.Net.Export.GeoPackage/
COPY tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj ./tests/Iwxxm.Net.Tests/
COPY tests/Iwxxm.Net.Validation.Saxon.Tests/Iwxxm.Net.Validation.Saxon.Tests.csproj ./tests/Iwxxm.Net.Validation.Saxon.Tests/
COPY tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj ./tests/Iwxxm.Net.Export.GeoPackage.Tests/
COPY tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj ./tests/Iwxxm.Net.Benchmarks/

RUN dotnet restore Iwxxm.Net.slnx

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
    --results-directory /workspace/artifacts/test-results && \
    dotnet test tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj \
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

RUN for project in src/Iwxxm.Net/Iwxxm.Net.csproj src/Iwxxm.Net.Validation.Saxon/Iwxxm.Net.Validation.Saxon.csproj src/Iwxxm.Net.Export.GeoPackage/Iwxxm.Net.Export.GeoPackage.csproj; do \
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

# Guard: the symbol package keeps SourceLink (provided by the SDK; needs .git in the build context, see .dockerignore)
RUN perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
        'unzip $ARGV[0] => \my $pdb, Name => "lib/netstandard2.0/Iwxxm.Net.pdb" or die "pdb not found: $UnzipError\n"; \
         if ($pdb !~ m{raw\.githubusercontent\.com}) { print STDERR "ERROR: Iwxxm.Net.pdb has no SourceLink map\n"; exit 1 } \
         print "Iwxxm.Net snupkg: SourceLink present\n";' \
        /workspace/artifacts/Iwxxm.Net.${PACKAGE_VERSION}.snupkg

# Guard: the GeoPackage package ships its assembly and depends on the core and on Microsoft.Data.Sqlite (which brings
# the native SQLite binaries); the core must keep zero dependencies, which the guard above enforces
RUN perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
        'unzip $ARGV[0] => \my $nuspec, Name => "Iwxxm.Net.Export.GeoPackage.nuspec" or die "nuspec not found: $UnzipError\n"; \
         for my $id ("Iwxxm.Net", "Microsoft.Data.Sqlite") { \
             $nuspec =~ /<dependency\s+id="\Q$id\E"/ or die "ERROR: Iwxxm.Net.Export.GeoPackage does not depend on $id\n"; } \
         unzip $ARGV[0] => \my $dll, Name => "lib/netstandard2.0/Iwxxm.Net.Export.GeoPackage.dll" or die "ERROR: assembly missing: $UnzipError\n"; \
         print "Iwxxm.Net.Export.GeoPackage nuspec: depends on Iwxxm.Net and Microsoft.Data.Sqlite; netstandard2.0 assembly present\n";' \
        /workspace/artifacts/Iwxxm.Net.Export.GeoPackage.${PACKAGE_VERSION}.nupkg

# Guard: the GeoPackage package reads internals of the core (InternalsVisibleTo), which only the version it was built
# against grants, so its dependency on Iwxxm.Net must be exactly the version of the core package packed with it
RUN perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
        'unzip $ARGV[0] => \my $nuspec, Name => "Iwxxm.Net.Export.GeoPackage.nuspec" or die "nuspec not found: $UnzipError\n"; \
         $nuspec =~ /<dependency\s+id="Iwxxm\.Net"\s+version="([^"]*)"/ or die "ERROR: no version on the Iwxxm.Net dependency\n"; \
         my $raw = $1; (my $declared = $raw) =~ s/^[\[(]|[\])]$//g; \
         $declared eq $ARGV[1] or die "ERROR: Iwxxm.Net.Export.GeoPackage depends on Iwxxm.Net $raw, not on the packed version $ARGV[1]\n"; \
         print "Iwxxm.Net.Export.GeoPackage depends on Iwxxm.Net $declared, the version packed with it\n";' \
        /workspace/artifacts/Iwxxm.Net.Export.GeoPackage.${PACKAGE_VERSION}.nupkg ${PACKAGE_VERSION}

# Guard: all three packages ship the third-party notices and the licence at the package root (the Saxon package also the
# SchXslt licence)
RUN for pkg in Iwxxm.Net Iwxxm.Net.Validation.Saxon Iwxxm.Net.Export.GeoPackage; do \
        for entry in THIRD-PARTY-NOTICES.md LICENSE; do \
            perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
                'unzip $ARGV[0] => \my $data, Name => $ARGV[1] or die "ERROR: $ARGV[0] does not contain $ARGV[1]: $UnzipError\n"; \
                 length($data) > 0 or die "ERROR: $ARGV[1] is empty in $ARGV[0]\n"; \
                 print "$ARGV[1] present in $ARGV[0]\n";' \
                "/workspace/artifacts/${pkg}.${PACKAGE_VERSION}.nupkg" "${entry}" || exit 1; \
        done; \
    done && \
    perl -MIO::Uncompress::Unzip=unzip,\$UnzipError -e \
        'unzip $ARGV[0] => \my $data, Name => "licenses/SchXslt/LICENSE" or die "ERROR: SchXslt LICENSE missing: $UnzipError\n"; \
         print "licenses/SchXslt/LICENSE present\n";' \
        /workspace/artifacts/Iwxxm.Net.Validation.Saxon.${PACKAGE_VERSION}.nupkg

# ==============================================================================
# Stage 5: artifacts — Exportable scratch stage for host extraction
# Usage: podman build --target artifacts --output type=local,dest=./artifacts .   (docker build works the same)
# ==============================================================================
FROM scratch AS artifacts
COPY --from=pack /workspace/artifacts/ /

# ==============================================================================
# Stages 5a and 5b (optional, AUD-M17): the net48 target of the core test project. They are lettered additions to
# the numbered sequence (1 to 6), not renumbered stages, and are not part of the default build.
#   5a `net48-compile` compiles the net48 target of the core and of the GeoPackage test projects against the reference
#      assemblies (Microsoft.NETFramework.ReferenceAssemblies).
#   5b `net48-test` also runs both under Mono with the xunit console runner (a Mono approximation of .NET Framework;
#      the Windows CI job is the real .NET Framework run). The GeoPackage run checks that the netstandard2.0 package
#      loads its bundled native SQLite (runtimes/<rid>/native) from a net48 application; the native binary Mono loads is
#      the Linux one, so the Windows DLL lookup is only verified by the Windows CI job.
# Usage: podman build --target net48-compile .   |   podman build --target net48-test .   (docker build works the same)
# These stages sit before `ci` so that the default (last) stage is unchanged.
#
# The xunit console runner package is pinned by SHA-256 and verified before it is unpacked. To update it: change
# XUNIT_RUNNER_CONSOLE_VERSION, download https://www.nuget.org/api/v2/package/xunit.runner.console/<version>,
# run `sha256sum` on the file and put the result in XUNIT_RUNNER_CONSOLE_SHA256.
# ==============================================================================
FROM restore AS net48-compile
COPY . .
RUN for project in tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj; do \
        dotnet restore "$project" -p:IWXXM_TEST_NET48=true && \
        dotnet build "$project" \
            --configuration Release \
            --no-restore \
            --framework net48 \
            -p:IWXXM_TEST_NET48=true \
            -p:ContinuousIntegrationBuild=true || exit 1; \
    done

FROM net48-compile AS net48-test
ARG XUNIT_RUNNER_CONSOLE_VERSION=2.9.3
ARG XUNIT_RUNNER_CONSOLE_SHA256=d8775ce150583d262b8a05d3a380c3851b60d0077f2201877ed81668c9b51de3
RUN apt-get update -qq && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends mono-complete unzip curl ca-certificates && \
    rm -rf /var/lib/apt/lists/* && \
    curl -sSfL -o /tmp/xunit-runner.zip "https://www.nuget.org/api/v2/package/xunit.runner.console/${XUNIT_RUNNER_CONSOLE_VERSION}" && \
    echo "${XUNIT_RUNNER_CONSOLE_SHA256}  /tmp/xunit-runner.zip" | sha256sum -c - && \
    unzip -q /tmp/xunit-runner.zip -d /opt/xunit-runner
# Mono resolves the P/Invoke library name "dl" (used by SQLitePCLRaw to dlopen the bundled SQLite) to libdl.so, which
# glibc 2.34+ (Ubuntu 24.04) no longer ships: only libdl.so.2 exists. Map the name once for Mono (tested with Mono 6.8).
RUN perl -0pi -e 's{</configuration>}{  <dllmap dll="dl" target="libdl.so.2" os="!windows"/>\n</configuration>}' /etc/mono/config && \
    grep -q 'dllmap dll="dl"' /etc/mono/config
WORKDIR /workspace/tests/Iwxxm.Net.Tests/bin/Release/net48
RUN mono /opt/xunit-runner/tools/net472/xunit.console.exe Iwxxm.Net.Tests.dll -nologo -parallel none
WORKDIR /workspace/tests/Iwxxm.Net.Export.GeoPackage.Tests/bin/Release/net48
# -noshadow: the console runner otherwise copies the test assemblies to a temporary directory, without the
# runtimes/<rid>/native folder the bundled SQLite is loaded from.
RUN mono /opt/xunit-runner/tools/net472/xunit.console.exe Iwxxm.Net.Export.GeoPackage.Tests.dll -nologo -parallel none -noshadow

# ==============================================================================
# Stage 6: ci — Default runtime stage (exports artifacts only; never publishes)
# Publishing happens exclusively in the GitHub Actions workflow. A deliberate local
# publish requires `tools/run-local-ci.sh --publish` (see README); this image never
# reads registry credentials on its own.
# Usage (docker works the same as podman):
#   podman build -t iwxxm-net-ci .
#   podman run --rm -v "$(pwd)/artifacts:/out" iwxxm-net-ci
# ==============================================================================
FROM pack AS ci

CMD ["/bin/bash", "-c", "\
    echo '====================================================================' && \
    echo ' Iwxxm.Net Local CI/CD Pipeline Completed Successfully' && \
    echo '====================================================================' && \
    ls -lh /workspace/artifacts/*.nupkg /workspace/artifacts/*.snupkg && \
    if [ -d /out ]; then \
        cp -rf /workspace/artifacts/* /out/ && \
        echo 'Copied compiled NuGet packages and test coverage reports to /out'; \
    fi \
"]
