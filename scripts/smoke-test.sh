#!/usr/bin/env bash
set -euo pipefail

: "${CONTROL_PLANE_URL:?CONTROL_PLANE_URL is required}"
curl --fail --silent --show-error --retry 5 --retry-delay 3 "${CONTROL_PLANE_URL%/}/health"
curl --fail --silent --show-error --retry 5 --retry-delay 3 "${CONTROL_PLANE_URL%/}/health/ready"
