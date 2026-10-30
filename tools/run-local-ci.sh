#!/usr/bin/env bash
# Runs the containerised CI pipeline locally and exports the packages to ./artifacts.
#
# The pipeline never publishes by default. Publishing is reserved for the GitHub Actions
# workflow. A deliberate local publish requires ALL of:
#   - the explicit `--publish` flag,
#   - a dedicated API key in IWXXM_NUGET_PUBLISH_API_KEY (GITHUB_TOKEN is ignored on purpose),
#   - an explicit PACKAGE_VERSION (no default version is ever published),
#   - a clean git working tree (the image copies the whole working directory).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

PUBLISH=false
for arg in "$@"; do
  case "${arg}" in
    --publish) PUBLISH=true ;;
    -h|--help)
      echo "Usage: bash tools/run-local-ci.sh [--publish]"
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 2
      ;;
  esac
done

if [[ "${PUBLISH}" == "true" ]]; then
  if [[ -z "${IWXXM_NUGET_PUBLISH_API_KEY:-}" ]]; then
    echo "Refusing to publish: IWXXM_NUGET_PUBLISH_API_KEY is not set (GITHUB_TOKEN is never used)." >&2
    exit 1
  fi
  if [[ -z "${PACKAGE_VERSION:-}" ]]; then
    echo "Refusing to publish: set an explicit PACKAGE_VERSION." >&2
    exit 1
  fi
  if ! git -C "${REPO_ROOT}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Refusing to publish: ${REPO_ROOT} is not a git working tree." >&2
    exit 1
  fi
  if [[ -n "$(git -C "${REPO_ROOT}" status --porcelain --untracked-files=all)" ]]; then
    echo "Refusing to publish: the git working tree is dirty (commit or stash every change first)." >&2
    exit 1
  fi
fi

BUILD_NUMBER="${BUILD_NUMBER:-1}"
PACKAGE_VERSION="${PACKAGE_VERSION:-1.0.0-rc.${BUILD_NUMBER}}"
FILE_VERSION="${FILE_VERSION:-1.0.0.${BUILD_NUMBER}}"
IMAGE_TAG="iwxxm-net-ci:${PACKAGE_VERSION}"
CONTAINER_ENGINE="${CONTAINER_ENGINE:-docker}"

mkdir -p "${REPO_ROOT}/artifacts"

echo "======================================================================"
echo " Running Local CI/CD Pipeline via ${CONTAINER_ENGINE} (v${PACKAGE_VERSION})"
echo " Phases: restore -> build -> test -> pack (publish: ${PUBLISH})"
echo "======================================================================"

"${CONTAINER_ENGINE}" build \
  --build-arg PACKAGE_VERSION="${PACKAGE_VERSION}" \
  --build-arg FILE_VERSION="${FILE_VERSION}" \
  -t "${IMAGE_TAG}" \
  "${REPO_ROOT}"

"${CONTAINER_ENGINE}" run --rm \
  -v "${REPO_ROOT}/artifacts:/out" \
  "${IMAGE_TAG}"

if [[ "${PUBLISH}" == "true" ]]; then
  GITHUB_OWNER="${GITHUB_OWNER:-0neTX}"
  echo "Publishing ${PACKAGE_VERSION} to GitHub Packages (owner: ${GITHUB_OWNER})..."
  "${CONTAINER_ENGINE}" run --rm \
    -e IWXXM_NUGET_PUBLISH_API_KEY \
    -e GITHUB_OWNER="${GITHUB_OWNER}" \
    "${IMAGE_TAG}" \
    /bin/bash -c 'dotnet nuget push "/workspace/artifacts/*.nupkg" \
      --source "https://nuget.pkg.github.com/${GITHUB_OWNER}/index.json" \
      --api-key "${IWXXM_NUGET_PUBLISH_API_KEY}"'
fi
