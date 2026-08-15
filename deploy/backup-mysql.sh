#!/usr/bin/env bash
# /opt/erdi-erc/backup-mysql.sh
# Tägliches MySQL-Dump-Backup mit 14-Tage-Retention.
#
# Voraussetzung: ~/.my.cnf des ausführenden Users enthält:
#   [client]
#   user=erdierc_backup
#   password=<read-only-passwort>
#   host=<PROD_HOST>
#
# chmod 600 ~/.my.cnf

set -euo pipefail

DB_NAME="erdierc"
BACKUP_DIR="/var/backups/erdi-erc"
RETENTION_DAYS=14
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUT_FILE="${BACKUP_DIR}/${DB_NAME}-${TIMESTAMP}.sql.gz"

mkdir -p "$BACKUP_DIR"

mysqldump \
  --single-transaction \
  --quick \
  --routines \
  --triggers \
  --events \
  --default-character-set=utf8mb4 \
  --no-tablespaces \
  "$DB_NAME" \
  | gzip -9 > "$OUT_FILE"

# Verifizieren, dass die Datei nicht leer ist
if [ ! -s "$OUT_FILE" ]; then
  echo "[ERROR] Backup-Datei ist leer: $OUT_FILE" >&2
  rm -f "$OUT_FILE"
  exit 1
fi

# Alte Backups aufräumen
find "$BACKUP_DIR" -name "${DB_NAME}-*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete

echo "[OK] Backup geschrieben: $OUT_FILE ($(du -h "$OUT_FILE" | cut -f1))"
