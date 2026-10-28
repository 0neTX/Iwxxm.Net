#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

BUILD_NUMBER="${BUILD_NUMBER:-1}"
PACKAGE_VERSION="${PACKAGE_VERSION:-1.0.0-rc.${BUILD_NUMBER}}"
FILE_VERSION="${FILE_VERSION:-1.0.0.${BUILD_NUMBER}}"
IMAGE_TAG="iwxxm-net-ci:${PACKAGE_VERSION}"

mkdir -p "${REPO_ROOT}/artifacts"

echo "======================================================================"
echo " Running Local CI/CD Pipeline via Docker (v${PACKAGE_VERSION})"
echo " Phases: restore -> architecture-audit -> build -> test -> pack"
echo "======================================================================"

docker build \
  --build-arg PACKAGE_VERSION="${PACKAGE_VERSION}" \
  --build-arg FILE_VERSION="${FILE_VERSION}" \
  -t "${IMAGE_TAG}" \
  "${REPO_ROOT}"

docker run --rm \
  -v "${REPO_ROOT}/artifacts:/out" \
  -e GITHUB_OWNER="${GITHUB_OWNER:-0neTX}" \
  -e GITHUB_TOKEN="${GITHUB_TOKEN:-}" \
  "${IMAGE_TAG}"
