***REMOVED***!/usr/bin/env bash
***REMOVED*** /opt/erdi-erc/backup-mysql.sh
***REMOVED*** Tägliches MySQL-Dump-Backup mit 14-Tage-Retention.
***REMOVED***
***REMOVED*** Voraussetzung: ~/.my.cnf des ausführenden Users enthält:
***REMOVED***   [client]
***REMOVED***   user=erdierc_backup
***REMOVED***   password=<read-only-passwort>
***REMOVED***   host=<PROD_HOST>
***REMOVED***
***REMOVED*** chmod 600 ~/.my.cnf

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

***REMOVED*** Verifizieren, dass die Datei nicht leer ist
if [ ! -s "$OUT_FILE" ]; then
  echo "[ERROR] Backup-Datei ist leer: $OUT_FILE" >&2
  rm -f "$OUT_FILE"
  exit 1
fi

***REMOVED*** Alte Backups aufräumen
find "$BACKUP_DIR" -name "${DB_NAME}-*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete

echo "[OK] Backup geschrieben: $OUT_FILE ($(du -h "$OUT_FILE" | cut -f1))"
