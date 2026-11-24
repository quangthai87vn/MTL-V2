#!/usr/bin/env sh
set -eu

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
COMPOSE_FILE=${COMPOSE_FILE:-$PROJECT_DIR/compose.prod.yml}
ENV_FILE=${ENV_FILE:-$PROJECT_DIR/.env}
IMAGE_TAG=${1:-main}

cd "$PROJECT_DIR"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi

export IMAGE_TAG

"$PROJECT_DIR/scripts/backup-postgres.sh"

if ! docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" pull; then
  cat >&2 <<'EOF'

Could not pull one or more container images.
If GHCR reports "unauthorized":
  1. Use a GitHub personal access token (classic) with read:packages.
  2. Run: docker login ghcr.io -u quangthai87vn
  3. Run this deploy script as the same Linux user, without sudo.

Docker credentials are user-specific. A login made as your normal user is not
available when the deploy script is later executed with sudo as root.
EOF
  exit 1
fi

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --remove-orphans --wait
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps

echo "MTL.V2 deployed with image tag: $IMAGE_TAG"
