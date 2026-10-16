#!/usr/bin/env bash
# Photon-Bypass first-release deploy script (docs/19-deploy-runbook.md).
#
# Stages:
#   db      apply the fresh-install SQL batch to MSSQL, then create the admin
#           account when the database is still empty (first release)
#   admin   create/verify the admin account only (later runs)
#   publish / ftp    reserved for the future file-upload stage (not implemented)
#
# Usage: scripts/deploy.sh db|admin [options]
#   --server|-S <host>     SQL Server host (env DEPLOY_SQL_SERVER)
#   --port <n>             port (env DEPLOY_SQL_PORT)
#   --user|-U <login>      SQL login (env DEPLOY_SQL_USER)
#   --password|-P <secret> SQL password (env DEPLOY_SQL_PASSWORD; prompted if missing)
#   --secrets <path>       read LocalDatabaseOptions:ConnectionString from a local
#                          copy of private/secrets.json — takes priority over the
#                          options above; the file is never echoed or logged
#   --admin-user <name>    non-interactive admin creation (env DEPLOY_ADMIN_USER)
#   --admin-password <s>   non-interactive admin password (env DEPLOY_ADMIN_PASSWORD)
#   --no-admin             skip admin creation in the db stage
#   --yes                  no confirmation prompt
#
# Secrets hygiene: passwords travel via environment variables (SQLCMDPASSWORD,
# DEPLOY_ADMIN_PASSWORD, DEPLOY_CONNECTION_STRING) — never on command lines,
# never in the log file.
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd -- "$SCRIPT_DIR/.." && pwd)
SQL_DIR="$REPO_ROOT/Database/LocalDatabase"
CLI_PROJECT="$REPO_ROOT/Backend/Tools/DeployCli/DeployCli.csproj"
LOG_DIR="$SCRIPT_DIR/logs"

# Ordered manifest of the fresh-install set (FK dependencies; Database.sql runs
# in the master context, every other file starts with USE FastBypass).
# The guard below aborts on any .sql in Database/LocalDatabase that is not
# listed here, so new scripts cannot silently miss production deploys.
MANIFEST="Database.sql Realm.sql Price.sql Account.sql Server.sql Wallet.sql \
History.sql ResetPassword.sql TrafficData.sql Renewal.sql Invoice.sql \
InvoiceSequence.sql Indexes.sql TotalPlanState.sql PlanState.sql"

# Remote paths a future publish/ftp stage must never overwrite or delete
# (docs/19 §د; full ownership table in Database/LocalDatabase/README.md):
#   private/                operator — M29 production secrets (never an FTP target)
#   data-protection-keys/   app (M12) — loss means re-entering encrypted configs
#   config/                 operator — default.ovpn / private key
#   events-*.log            app (Serilog) — rolling logs
# appsettings.json is deploy-managed (committed test values; production values
# arrive per-key from private/secrets.json — M29).
PRESERVE="private/ data-protection-keys/ config/ events-*.log"

STAGE=""
SQL_SERVER=${DEPLOY_SQL_SERVER:-}
SQL_PORT=${DEPLOY_SQL_PORT:-}
SQL_USER=${DEPLOY_SQL_USER:-}
SQL_PASSWORD=${DEPLOY_SQL_PASSWORD:-}
SECRETS_PATH=""
ASSUME_YES=false
NO_ADMIN=false
ADMIN_USER=${DEPLOY_ADMIN_USER:-}
ADMIN_PASSWORD=${DEPLOY_ADMIN_PASSWORD:-}

FAILED=false
LOG_FILE=""

log() { printf '%s\n' "$*" | tee -a "$LOG_FILE"; }

die() {
    FAILED=true
    printf 'FAIL: %s\n' "$*" | tee -a "$LOG_FILE" >&2
    exit 1
}

usage() {
    cat <<'EOF'
usage: scripts/deploy.sh <stage> [options]

stages:
  db        apply the fresh-install SQL batch, then create the admin if the
            Account table is empty (first release)
  admin     create/verify the admin account only
  publish   reserved for the future upload stage (not implemented yet)
  ftp       reserved for the future upload stage (not implemented yet)

options:
  --server|-S <host>       SQL Server host            (env DEPLOY_SQL_SERVER)
  --port <n>               SQL Server port            (env DEPLOY_SQL_PORT)
  --user|-U <login>        SQL login                  (env DEPLOY_SQL_USER)
  --password|-P <secret>   SQL password               (env DEPLOY_SQL_PASSWORD)
  --secrets <path>         local copy of private/secrets.json; its
                           LocalDatabaseOptions:ConnectionString takes priority
  --admin-user <name>      admin username, non-interactive (env DEPLOY_ADMIN_USER)
  --admin-password <s>     admin password, non-interactive (env DEPLOY_ADMIN_PASSWORD)
  --no-admin               skip admin creation in the db stage
  --yes                    skip the confirmation prompt

examples:
  scripts/deploy.sh db --secrets ../private/secrets.json
  scripts/deploy.sh db -S sql.example.com -U fastbypass -P '<password>'
  DEPLOY_ADMIN_USER=admin DEPLOY_ADMIN_PASSWORD='<password>' scripts/deploy.sh admin \
      --secrets ../private/secrets.json
EOF
}

# --- connection resolution ---------------------------------------------------

# Extract Server / User Id / Password from LocalDatabaseOptions:ConnectionString
# in a local copy of private/secrets.json (M29). The file is read, never logged.
load_secrets() {
    [ -f "$SECRETS_PATH" ] || die "secrets file not found: $SECRETS_PATH"

    local section connstr key value saved_ifs
    section=$(sed -n '/"LocalDatabaseOptions"/,/}/p' "$SECRETS_PATH")
    connstr=$(printf '%s\n' "$section" \
        | grep -o '"ConnectionString"[[:space:]]*:[[:space:]]*"[^"]*"' \
        | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')
    [ -n "$connstr" ] || die "could not read LocalDatabaseOptions:ConnectionString from $SECRETS_PATH"

    export DEPLOY_CONNECTION_STRING=$connstr

    saved_ifs=$IFS
    IFS=';'
    for pair in $connstr; do
        key=$(printf '%s' "${pair%%=*}" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | tr '[:upper:]' '[:lower:]')
        value=$(printf '%s' "${pair#*=}" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')
        case "$key" in
            server | "data source" | address | addr | "network address")
                SQL_SERVER=${value#tcp:} ;;
            "user id" | uid | user) SQL_USER=$value ;;
            password | pwd) SQL_PASSWORD=$value ;;
        esac
    done
    IFS=$saved_ifs
}

resolve_connection() {
    if [ -n "$SECRETS_PATH" ]; then
        load_secrets
    fi

    if [ -z "$SQL_PASSWORD" ] && [ -t 0 ]; then
        printf 'SQL password for %s: ' "${SQL_USER:-<login>}"
        stty -echo
        read -r SQL_PASSWORD
        stty echo
        printf '\n'
    fi

    [ -n "$SQL_SERVER" ] || die "SQL server is required (--server/-S, env DEPLOY_SQL_SERVER, or --secrets)"
    [ -n "$SQL_USER" ] || die "SQL user is required (--user/-U, env DEPLOY_SQL_USER, or --secrets)"
    [ -n "$SQL_PASSWORD" ] || die "SQL password is required (--password/-P, env DEPLOY_SQL_PASSWORD, or --secrets)"

    SQLCMD_SERVER=$SQL_SERVER
    [ -n "$SQL_PORT" ] && SQLCMD_SERVER="$SQL_SERVER,$SQL_PORT"

    # DeployCli (Microsoft.Data.SqlClient) needs a full connection string.
    # Production should use --secrets (the raw production string is kept above);
    # the composed fallback mirrors the committed dev values of appsettings.json.
    if [ -z "${DEPLOY_CONNECTION_STRING:-}" ]; then
        export DEPLOY_CONNECTION_STRING="Server=$SQLCMD_SERVER; Database=FastBypass; User Id=$SQL_USER; Password=$SQL_PASSWORD; Encrypt=False; TrustServerCertificate=True;"
    fi
}

# --- preflight ----------------------------------------------------------------

require_sqlcmd() {
    if ! command -v sqlcmd >/dev/null 2>&1; then
        die "sqlcmd not found — install it first:
  Ubuntu/Debian: sudo apt-get install sqlcmd   (Microsoft apt repo)
  Windows/winget: winget install SQLcmd        (also works in Git Bash)
  macOS: brew install sqlcmd
  or download go-sqlcmd: https://learn.microsoft.com/sql/tools/sqlcmd/download"
    fi
}

require_dotnet() {
    command -v dotnet >/dev/null 2>&1 || die "dotnet not found — required by DeployCli (create-admin/verify)"
}

# --- manifest guard ------------------------------------------------------------

guard_manifest() {
    local file path
    for file in $MANIFEST; do
        [ -f "$SQL_DIR/$file" ] || die "manifest file missing: $SQL_DIR/$file"
    done
    for path in "$SQL_DIR"/*.sql; do
        file=${path##*/}
        case " $MANIFEST " in
            *" $file "*) ;;
            *) die "unlisted script in Database/LocalDatabase: $file — add it to the manifest in scripts/deploy.sh (or remove the file)" ;;
        esac
    done
}

# --- sqlcmd helpers ------------------------------------------------------------

run_sql_file() {
    local file=$1
    log "applying $file"
    if ! SQLCMDPASSWORD="$SQL_PASSWORD" sqlcmd -b -t 600 -f 65001 \
            -S "$SQLCMD_SERVER" -U "$SQL_USER" -i "$file" 2>&1 | tee -a "$LOG_FILE"; then
        die "sqlcmd failed on $file (see $LOG_FILE)"
    fi
}

account_count() {
    local raw
    if ! raw=$(SQLCMDPASSWORD="$SQL_PASSWORD" sqlcmd -b -t 600 -f 65001 \
            -S "$SQLCMD_SERVER" -U "$SQL_USER" -d FastBypass \
            -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Account" 2>>"$LOG_FILE" | tee -a "$LOG_FILE"); then
        die "could not count Account rows (see $LOG_FILE)"
    fi
    raw=$(printf '%s' "$raw" | tr -cd '0-9')
    case "$raw" in
        '' | *[!0-9]*) die "could not parse the Account count" ;;
        *) printf '%s\n' "$raw" ;;
    esac
}

# --- admin creation ------------------------------------------------------------

prompt_admin() {
    if [ -z "$ADMIN_USER" ]; then
        [ -t 0 ] || die "admin username is required in non-interactive mode (--admin-user or env DEPLOY_ADMIN_USER)"
        printf 'admin username [admin]: '
        read -r ADMIN_USER
        ADMIN_USER=${ADMIN_USER:-admin}
    fi
    if [ -z "$ADMIN_PASSWORD" ]; then
        [ -t 0 ] || die "admin password is required in non-interactive mode (--admin-password or env DEPLOY_ADMIN_PASSWORD)"
        printf 'admin password: '
        stty -echo
        read -r ADMIN_PASSWORD
        stty echo
        printf '\n'
        printf 'confirm admin password: '
        stty -echo
        local confirm
        read -r confirm
        stty echo
        printf '\n'
        [ "$ADMIN_PASSWORD" = "$confirm" ] || die "admin passwords do not match"
        [ -n "$ADMIN_PASSWORD" ] || die "admin password must not be empty"
    fi
}

run_cli() {
    export DEPLOY_CONNECTION_STRING DEPLOY_ADMIN_PASSWORD
    dotnet run --project "$CLI_PROJECT" -- "$@"
}

create_and_verify_admin() {
    prompt_admin
    if ! run_cli create-admin --username "$ADMIN_USER" 2>&1 | tee -a "$LOG_FILE"; then
        die "DeployCli create-admin failed (see $LOG_FILE)"
    fi
    if ! run_cli verify --username "$ADMIN_USER" 2>&1 | tee -a "$LOG_FILE"; then
        die "DeployCli verify failed (see $LOG_FILE)"
    fi
}

# --- stages ---------------------------------------------------------------------

run_stage_db() {
    guard_manifest
    require_sqlcmd
    require_dotnet
    confirm
    log "stage db — server: $SQLCMD_SERVER, user: $SQL_USER, database: FastBypass"

    # Database.sql runs in the login's default context (master) because
    # CREATE DATABASE cannot be issued inside USE FastBypass; every other
    # file switches with its own USE FastBypass header.
    local file count=0
    for file in $MANIFEST; do
        run_sql_file "$SQL_DIR/$file"
    done

    count=$(account_count)
    if [ "$count" -eq 0 ]; then
        if [ "$NO_ADMIN" = true ]; then
            log "Account table is empty and --no-admin was given — create the admin later with: scripts/deploy.sh admin"
        else
            log "Account table is empty — first release, creating the admin account"
            create_and_verify_admin
        fi
    else
        log "Account table has $count row(s) — skipping admin creation (use the admin stage to manage it)"
    fi
}

run_stage_admin() {
    require_dotnet
    log "stage admin — server: $SQLCMD_SERVER, user: $SQL_USER"
    create_and_verify_admin
}

confirm() {
    [ "$ASSUME_YES" = true ] && return 0
    [ -t 0 ] || die "stdin is not a terminal — pass --yes for non-interactive runs"
    printf 'Apply the database batch to %s as %s? [y/N] ' "$SQLCMD_SERVER" "$SQL_USER"
    local answer
    read -r answer
    case "$answer" in
        y | Y | yes | YES) ;;
        *) die "aborted by user" ;;
    esac
}

# --- argument parsing ------------------------------------------------------------

parse_args() {
    while [ $# -gt 0 ]; do
        case "$1" in
            db | admin | publish | ftp)
                [ -z "$STAGE" ] || { printf 'error: multiple stages\n' >&2; exit 1; }
                STAGE=$1 ;;
            --server | -S)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                SQL_SERVER=$2; shift ;;
            --port)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                SQL_PORT=$2; shift ;;
            --user | -U)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                SQL_USER=$2; shift ;;
            --password | -P)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                SQL_PASSWORD=$2; shift ;;
            --secrets)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                SECRETS_PATH=$2; shift ;;
            --admin-user)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                ADMIN_USER=$2; shift ;;
            --admin-password)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                ADMIN_PASSWORD=$2; shift ;;
            --no-admin) NO_ADMIN=true ;;
            --yes | -y) ASSUME_YES=true ;;
            --help | -h)
                usage; exit 0 ;;
            *)
                printf 'error: unknown option %s\n' "$1" >&2
                usage; exit 1 ;;
        esac
        shift
    done
}

main() {
    parse_args "$@"
    if [ -z "$STAGE" ]; then
        usage >&2
        exit 1
    fi

    mkdir -p "$LOG_DIR"
    LOG_FILE="$LOG_DIR/deploy-$(date +%Y%m%d-%H%M%S).log"

    trap 'status=$?; if [ "$status" -ne 0 ] && [ "$FAILED" = false ]; then
            printf "FAIL: unexpected error (exit %s, log: %s)\n" "$status" "$LOG_FILE" >&2
        fi' EXIT

    case "$STAGE" in
        publish | ftp) die "stage '$STAGE' is reserved for the future upload workflow — not implemented yet" ;;
    esac

    resolve_connection
    log "photon-bypass deploy — stage: $STAGE, started: $(date '+%Y-%m-%d %H:%M:%S')"

    case "$STAGE" in
        db) run_stage_db ;;
        admin) run_stage_admin ;;
    esac

    log "reminder: back up the data-protection-keys folder after the first app start (docs/14 M12) — losing it forces re-entering encrypted server configs"
    log "OK — stage $STAGE finished (log: $LOG_FILE)"
}

main "$@"
