#!/usr/bin/env sh
set -eu

usage() {
  cat <<'EOF'
Usage:
  ./scripts/import-data-protection-keys.sh KEY_BUNDLE --confirm-import

Imports a trusted Data Protection key bundle into the production key volume.
Existing keys are backed up and retained so active cookies and server-encrypted
values continue to work. The database is not modified.
EOF
}

if [ "$#" -ne 2 ] || [ "$2" != "--confirm-import" ]; then
  usage >&2
  exit 2
fi

KEY_BUNDLE=$1
PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
COMPOSE_FILE=${COMPOSE_FILE:-$PROJECT_DIR/compose.prod.yml}
ENV_FILE=${ENV_FILE:-$PROJECT_DIR/.env}
KEY_VOLUME=${DATA_PROTECTION_VOLUME:-mtl-v2_data-protection-keys}

case "$KEY_BUNDLE" in
  /*) ;;
  *) KEY_BUNDLE=$(CDPATH= cd -- "$(dirname -- "$KEY_BUNDLE")" && pwd)/$(basename -- "$KEY_BUNDLE") ;;
esac

if [ ! -s "$KEY_BUNDLE" ]; then
  echo "Data Protection key bundle is missing or empty: $KEY_BUNDLE" >&2
  exit 1
fi
if [ ! -f "$ENV_FILE" ]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi
if grep -q 'CHANGE_ME_' "$ENV_FILE"; then
  echo "Replace every CHANGE_ME_ value in $ENV_FILE before importing keys." >&2
  exit 1
fi

CHECKSUM_FILE=$KEY_BUNDLE.sha256
if [ -f "$CHECKSUM_FILE" ]; then
  (
    cd "$(dirname -- "$KEY_BUNDLE")"
    sha256sum -c "$(basename -- "$CHECKSUM_FILE")"
  )
else
  echo "Warning: checksum file not found: $CHECKSUM_FILE" >&2
fi

ENTRIES=$(tar -tzf "$KEY_BUNDLE")
if ! printf '%s\n' "$ENTRIES" | grep -Eq '(^|/)key-[0-9a-fA-F-]+\.xml$'; then
  echo "The bundle does not contain a Data Protection key file." >&2
  exit 1
fi
if printf '%s\n' "$ENTRIES" | grep -Evq '^(\./)?$|^(\./)?key-[0-9a-fA-F-]+\.xml$'; then
  echo "The bundle contains an unexpected or unsafe path." >&2
  exit 1
fi

export IMAGE_TAG=${IMAGE_TAG:-main}
cd "$PROJECT_DIR"

compose() {
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

compose up -d postgres
docker volume inspect "$KEY_VOLUME" >/dev/null

API_UID=$(compose run --rm --no-deps --entrypoint sh api -c 'id -u')
API_GID=$(compose run --rm --no-deps --entrypoint sh api -c 'id -g')
BACKUP_DIR=$PROJECT_DIR/backups/data-protection
BACKUP_NAME=server-keys-before-import-$(date -u +%Y%m%d-%H%M%S).tar.gz
mkdir -p "$BACKUP_DIR"

echo "Backing up the current server key ring..."
docker run --rm \
  --mount "type=volume,source=$KEY_VOLUME,target=/keys,readonly" \
  --mount "type=bind,source=$BACKUP_DIR,target=/backup" \
  postgres:18-alpine \
  sh -ec "tar -C /keys -czf '/backup/$BACKUP_NAME' ."

echo "Stopping API and web containers..."
compose stop web api || true

IMPORT_COMPLETE=0
on_exit() {
  status=$?
  trap - EXIT HUP INT TERM
  if [ "$IMPORT_COMPLETE" -eq 0 ]; then
    compose up -d --remove-orphans --wait || true
  fi
  exit "$status"
}
trap on_exit EXIT HUP INT TERM

echo "Importing Data Protection keys without removing existing server keys..."
docker run --rm \
  --env API_UID="$API_UID" \
  --env API_GID="$API_GID" \
  --mount "type=volume,source=$KEY_VOLUME,target=/keys" \
  --mount "type=bind,source=$KEY_BUNDLE,target=/transfer/keys.tar.gz,readonly" \
  postgres:18-alpine \
  sh -ec 'tar -C /keys -xzf /transfer/keys.tar.gz; chown "$API_UID:$API_GID" /keys/key-*.xml; chmod 600 /keys/key-*.xml'

compose up -d --remove-orphans --wait
IMPORT_COMPLETE=1
trap - EXIT HUP INT TERM

compose ps
echo "Data Protection keys imported successfully."
echo "Server key backup: $BACKUP_DIR/$BACKUP_NAME"
