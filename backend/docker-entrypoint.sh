#!/bin/sh
# Starts the API (or any command passed as arguments, e.g. ./efbundle) as the non-root
# "app" user.
#
# Railway mounts volumes as root, so on Railway the container is started as root
# (RAILWAY_RUN_UID=0): this script then prepares /app/data on the volume, hands it to
# "app" and drops privileges before running the command. Started as "app" (Docker Compose,
# where named volumes inherit the image's ownership), it only checks that the data
# directories are writable.
set -eu

DATA_DIR=/app/data
APP_UID="${APP_UID:-1654}"

# Railway injects PORT; locally the image default (8080) applies.
export ASPNETCORE_HTTP_PORTS="${PORT:-${ASPNETCORE_HTTP_PORTS:-8080}}"

if [ "$#" -eq 0 ]; then
  set -- dotnet Imotek.API.dll
fi

if [ "$(id -u)" = "0" ]; then
  mkdir -p "$DATA_DIR/uploads" "$DATA_DIR/keys"
  # Only walk the tree when ownership is wrong (first start on a fresh volume).
  if [ "$(stat -c %u "$DATA_DIR")" != "$APP_UID" ] || [ "$(stat -c %u "$DATA_DIR/uploads")" != "$APP_UID" ] || [ "$(stat -c %u "$DATA_DIR/keys")" != "$APP_UID" ]; then
    chown -R "$APP_UID:$APP_UID" "$DATA_DIR"
  fi
  chmod 700 "$DATA_DIR/keys"
  # setpriv keeps the environment: point HOME at the app user's home, where .NET
  # single-file apps such as the migration bundle extract themselves.
  export HOME=/home/app
  exec setpriv --reuid="$APP_UID" --regid="$APP_UID" --clear-groups -- "$@"
fi

for dir in "$DATA_DIR/uploads" "$DATA_DIR/keys"; do
  if ! mkdir -p "$dir" 2>/dev/null || [ ! -w "$dir" ]; then
    echo "imotek: $dir is not writable by uid $(id -u). On Railway, set RAILWAY_RUN_UID=0 so the entrypoint can prepare the volume." >&2
    exit 1
  fi
done

exec "$@"
