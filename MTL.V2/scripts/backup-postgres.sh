#!/usr/bin/env sh
set -eu

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
COMPOSE_FILE=${COMPOSE_FILE:-$PROJECT_DIR/compose.prod.yml}
ENV_FILE=${ENV_FILE:-$PROJECT_DIR/.env}
BACKUP_DIR=${BACKUP_DIR:-$PROJECT_DIR/backups/postgres}
RETENTION_DAYS=${RETENTION_DAYS:-14}

cd "$PROJECT_DIR"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi

if ! docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps --status running --services | grep -qx postgres; then
  echo "PostgreSQL is not running; skipping backup."
  exit 0
fi

mkdir -p "$BACKUP_DIR"
STAMP=$(date -u +%Y%m%d-%H%M%S)
BACKUP_FILE="$BACKUP_DIR/mtl-v2-$STAMP.dump"

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
  sh -c 'pg_dump --format=custom --no-owner --no-privileges --username="$POSTGRES_USER" "$POSTGRES_DB"' \
  > "$BACKUP_FILE"

find "$BACKUP_DIR" -type f -name 'mtl-v2-*.dump' -mtime "+$RETENTION_DAYS" -delete
echo "Database backup created: $BACKUP_FILE"
