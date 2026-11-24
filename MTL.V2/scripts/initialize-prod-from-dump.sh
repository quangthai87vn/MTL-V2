#!/usr/bin/env sh
set -eu

usage() {
  cat <<'EOF'
Usage:
  ./scripts/initialize-prod-from-dump.sh DUMP_FILE IMAGE_TAG --confirm-overwrite

Example:
  ./scripts/initialize-prod-from-dump.sh \
    /opt/mtl-v2/incoming/mtl-v2-local-20261124-180000.dump \
    main \
    --confirm-overwrite

This command replaces the complete production PostgreSQL database with the
contents of DUMP_FILE. It creates a server-side backup before restoring.
EOF
}

if [ "$#" -ne 3 ] || [ "$3" != "--confirm-overwrite" ]; then
  usage >&2
  exit 2
fi

DUMP_FILE=$1
IMAGE_TAG=$2
PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
COMPOSE_FILE=${COMPOSE_FILE:-$PROJECT_DIR/compose.prod.yml}
ENV_FILE=${ENV_FILE:-$PROJECT_DIR/.env}

case "$DUMP_FILE" in
  /*) ;;
  *) DUMP_FILE=$(CDPATH= cd -- "$(dirname -- "$DUMP_FILE")" && pwd)/$(basename -- "$DUMP_FILE") ;;
esac

if [ ! -s "$DUMP_FILE" ]; then
  echo "Database dump is missing or empty: $DUMP_FILE" >&2
  exit 1
fi
if [ ! -f "$ENV_FILE" ]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi
if grep -q 'CHANGE_ME_' "$ENV_FILE"; then
  echo "Replace every CHANGE_ME_ value in $ENV_FILE before initialization." >&2
  exit 1
fi

CHECKSUM_FILE=$DUMP_FILE.sha256
if [ -f "$CHECKSUM_FILE" ]; then
  (
    cd "$(dirname -- "$DUMP_FILE")"
    sha256sum -c "$(basename -- "$CHECKSUM_FILE")"
  )
else
  echo "Warning: checksum file not found: $CHECKSUM_FILE" >&2
fi

export IMAGE_TAG
cd "$PROJECT_DIR"

compose() {
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

echo "Pulling application images tagged: $IMAGE_TAG"
compose pull

echo "Starting PostgreSQL..."
compose up -d --wait postgres

echo "Validating PostgreSQL dump..."
compose exec -T postgres pg_restore --list < "$DUMP_FILE" > /dev/null

echo "Backing up the current server database..."
COMPOSE_FILE=$COMPOSE_FILE ENV_FILE=$ENV_FILE "$PROJECT_DIR/scripts/backup-postgres.sh"

echo "Stopping API and web containers..."
compose stop web api

RESTORE_COMPLETE=0
on_exit() {
  status=$?
  trap - EXIT HUP INT TERM
  if [ "$status" -ne 0 ] && [ "$RESTORE_COMPLETE" -eq 0 ]; then
    echo "Initialization failed. Restarting the application with the database state left by the transactional restore." >&2
    compose up -d --remove-orphans --wait || true
  fi
  exit "$status"
}
trap on_exit EXIT HUP INT TERM

echo "Replacing the server database with: $DUMP_FILE"
compose exec -T postgres sh -c 'pg_restore \
  --clean \
  --if-exists \
  --single-transaction \
  --exit-on-error \
  --no-owner \
  --no-privileges \
  --username="$POSTGRES_USER" \
  --dbname="$POSTGRES_DB"' < "$DUMP_FILE"

echo "Starting the complete application..."
compose up -d --remove-orphans --wait
RESTORE_COMPLETE=1
trap - EXIT HUP INT TERM

compose ps
echo "Production initialization completed from: $DUMP_FILE"
echo "Application image tag: $IMAGE_TAG"
