#!/usr/bin/env bash
# Photon-Bypass first-release deploy script (Docs/19-deploy-runbook.md).
#
# Stages:
#   db      apply the fresh-install SQL batch to MSSQL, then create the admin
#           account when the database is still empty (first release)
#   admin   create/verify the admin account only (later runs)
#   publish / ftp    reserved for the future file-upload stage (not implemented)
#
# Usage: Scripts/deploy.sh db|admin [options]
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
#   --database <name>      target database name (env DEPLOY_DATABASE; default
#                          FastBypass, or the Database= value from --secrets) —
#                          substituted into USE/CREATE like the test host does
#   --no-create-db         skip Database.sql (the database must already exist)
#   --yes                  no confirmation prompt
#
# Secrets hygiene: passwords travel via environment variables (SQLCMDPASSWORD,
# DEPLOY_ADMIN_PASSWORD, DEPLOY_CONNECTION_STRING) — never on command lines,
# never in the log file.
#
# eample:
# cd /home/ryan/Developing/Internet/Radius/photon-bypass/Scripts
# ./deploy.sh db --secrets ../Backend/Portal/appsettings.Development.json --admin-user admin --admin-password admin
#
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
MANIFEST="Database.sql Realm.sql Price.sql PanelUser.sql Server.sql VpnAccount.sql \
Wallet.sql History.sql ResetPassword.sql TelegramLink.sql TrafficData.sql Renewal.sql \
Invoice.sql InvoiceSequence.sql Indexes.sql TotalPlanState.sql PlanState.sql"

# Remote paths a future publish/ftp stage must never overwrite or delete
# (Docs/19 §د; full ownership table in Database/LocalDatabase/README.md):
#   private/                operator — M29 production secrets (never an FTP target)
#   data-protection-keys/   app (M12) — loss means re-entering encrypted configs
#   config/                 operator — default.ovpn / private key
#   events-*.log            app (Serilog) — rolling logs
# appsettings.json is deploy-managed (base file carries no secrets; test
# fixtures live in appsettings.Development.json; production values arrive
# per-key from private/secrets.json — M29).
PRESERVE="private/ data-protection-keys/ config/ events-*.log"

STAGE=""
SQL_SERVER=${DEPLOY_SQL_SERVER:-}
SQL_PORT=${DEPLOY_SQL_PORT:-}
SQL_USER=${DEPLOY_SQL_USER:-}
SQL_PASSWORD=${DEPLOY_SQL_PASSWORD:-}
SECRETS_PATH=""
ASSUME_YES=false
NO_ADMIN=false
NO_CREATE_DB=false
DEPLOY_DATABASE=${DEPLOY_DATABASE:-}
SECRETS_DB=""
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
usage: Scripts/deploy.sh <stage> [options]

stages:
  db        apply the fresh-install SQL batch, then create the admin if the
            PanelUser table is empty (first release)
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
  --database <name>        target database name (env DEPLOY_DATABASE; default
                           FastBypass, or the Database= value from --secrets)
  --no-create-db           skip Database.sql — the database must already exist
  --yes                    skip the confirmation prompt

examples:
  Scripts/deploy.sh db --secrets ../private/secrets.json
  Scripts/deploy.sh db -S sql.example.com -U fastbypass -P '<password>'
  Scripts/deploy.sh db -S localhost -U test -P '<password>' --database MyPanel --no-create-db
  DEPLOY_ADMIN_USER=admin DEPLOY_ADMIN_PASSWORD='<password>' Scripts/deploy.sh admin \
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
            database | "initial catalog") SECRETS_DB=$value ;;
        esac
    done
    IFS=$saved_ifs
}

resolve_connection() {
    if [ -n "$SECRETS_PATH" ]; then
        load_secrets
    fi

    # Effective target database: explicit --database wins; otherwise the
    # Database= value from --secrets; otherwise the default. An explicit name
    # that contradicts --secrets fails fast instead of splitting the run
    # between two databases (sqlcmd -d vs the DeployCli connection string).
    if [ -n "$DEPLOY_DATABASE" ] && [ -n "$SECRETS_DB" ] && [ "$DEPLOY_DATABASE" != "$SECRETS_DB" ]; then
        die "database name mismatch: --database='$DEPLOY_DATABASE' but the --secrets connection string targets '$SECRETS_DB'"
    fi
    DEPLOY_DATABASE=${DEPLOY_DATABASE:-${SECRETS_DB:-FastBypass}}
    case "$DEPLOY_DATABASE" in
        '' | *[!A-Za-z0-9_]*)
            die "invalid database name '$DEPLOY_DATABASE' — only letters, digits and underscore are allowed" ;;
        [0-9]*)
            die "invalid database name '$DEPLOY_DATABASE' — must start with a letter or underscore" ;;
    esac

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
        export DEPLOY_CONNECTION_STRING="Server=$SQLCMD_SERVER; Database=$DEPLOY_DATABASE; User Id=$SQL_USER; Password=$SQL_PASSWORD; Encrypt=False; TrustServerCertificate=True;"
    fi
}

# --- preflight ----------------------------------------------------------------

require_sqlcmd() {
    if ! command -v sqlcmd >/dev/null 2>&1 && ! command -v sqlcmd.exe >/dev/null 2>&1; then
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
            *) die "unlisted script in Database/LocalDatabase: $file — add it to the manifest in Scripts/deploy.sh (or remove the file)" ;;
        esac
    done
}

# --- sqlcmd helpers ------------------------------------------------------------

# Paths handed to the native Windows sqlcmd must be Windows-form: the MSYS
# auto-conversion does not survive the winget sqlcmd launcher shim, and the
# POSIX /d/... path degrades to a bare drive ("Access is denied" on file D:).
# Same pattern as the wp()/wslpath translation in Scripts/xui-test-vm.sh.
native_path() { # POSIX path -> Windows form when the Windows sqlcmd is in use
    local resolved
    resolved=$(command -v sqlcmd 2>/dev/null || command -v sqlcmd.exe 2>/dev/null || printf '%s' sqlcmd)
    case "$(uname -s):$resolved" in
        MSYS*:* | MINGW*:*) ;;              # Git Bash: sqlcmd is the Windows binary
        *:*.exe | *:*.EXE) ;;               # WSL/Linux invoking sqlcmd.exe explicitly
        *) printf '%s\n' "$1"; return 0 ;;  # native sqlcmd: POSIX path as-is
    esac
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    elif command -v wslpath >/dev/null 2>&1; then
        wslpath -w "$1"
    else
        printf '%s\n' "$1"
    fi
}

run_sql_file() {
    local file=$1 apply_file
    # Every file is staged in a temp copy behind a standard ANSI prelude:
    # go-sqlcmd may open the session with QUOTED_IDENTIFIER OFF, which fails
    # the filtered unique indexes with Msg 1934, while the SQL set itself is
    # caller-agnostic (the test host's SqlClient connects with these ON).
    # With a custom database name the token FastBypass is rewritten in the
    # same pass — the same global replace the test host performs
    # (DatabaseScriptPrepare.ReplaceDatabaseName); the name was validated to
    # [A-Za-z0-9_] so it is safe inside both the sed replacement and the SQL.
    apply_file=$(mktemp "${TMPDIR:-/tmp}/photon-bypass-deploy-XXXXXX.sql")
    {
        printf 'SET QUOTED_IDENTIFIER ON;\nSET ANSI_NULLS ON;\nSET ANSI_PADDING ON;\nSET ANSI_WARNINGS ON;\nSET CONCAT_NULL_YIELDS_NULL ON;\nSET ARITHABORT ON;\nGO\n'
        if [ "$DEPLOY_DATABASE" != "FastBypass" ]; then
            sed "s/FastBypass/$DEPLOY_DATABASE/g" "$file"
        else
            cat "$file"
        fi
    } > "$apply_file"
    log "applying $file"
    if ! SQLCMDPASSWORD="$SQL_PASSWORD" sqlcmd -b -t 600 -f 65001 \
            -S "$SQLCMD_SERVER" -U "$SQL_USER" -i "$(native_path "$apply_file")" 2>&1 | tee -a "$LOG_FILE"; then
        die "sqlcmd failed on $file (see $LOG_FILE)"
    fi
    rm -f "$apply_file"
}

account_count() {
    local raw
    if ! raw=$(SQLCMDPASSWORD="$SQL_PASSWORD" sqlcmd -b -t 600 -f 65001 \
            -S "$SQLCMD_SERVER" -U "$SQL_USER" -d "$DEPLOY_DATABASE" \
            -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.PanelUser" 2>>"$LOG_FILE" | tee -a "$LOG_FILE"); then
        die "could not count PanelUser rows (see $LOG_FILE)"
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
    # ADMIN_PASSWORD is filled by --admin-password, the interactive prompt, or
    # the DEPLOY_ADMIN_PASSWORD env default; DeployCli reads the env var of the
    # same name — bridge it here so the password never travels on a command line.
    DEPLOY_ADMIN_PASSWORD=$ADMIN_PASSWORD
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
    log "stage db — server: $SQLCMD_SERVER, user: $SQL_USER, database: $DEPLOY_DATABASE"

    # Database.sql runs in the login's default context (master) because
    # CREATE DATABASE cannot be issued inside USE FastBypass; every other
    # file switches with its own USE FastBypass header. With --no-create-db
    # it is skipped entirely — the database must already exist (e.g. created
    # from the hosting panel when the login lacks CREATE DATABASE rights).
    local file count=0
    for file in $MANIFEST; do
        if [ "$NO_CREATE_DB" = true ] && [ "$file" = "Database.sql" ]; then
            log "skipping Database.sql (--no-create-db) — '$DEPLOY_DATABASE' must already exist"
            continue
        fi
        run_sql_file "$SQL_DIR/$file"
    done

    count=$(account_count)
    if [ "$count" -eq 0 ]; then
        if [ "$NO_ADMIN" = true ]; then
            log "PanelUser table is empty and --no-admin was given — create the admin later with: Scripts/deploy.sh admin"
        else
            log "PanelUser table is empty — first release, creating the admin account"
            create_and_verify_admin
        fi
    else
        log "PanelUser table has $count row(s) — skipping admin creation (use the admin stage to manage it)"
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
            --database)
                [ $# -ge 2 ] || { printf 'error: missing value for %s\n' "$1" >&2; exit 1; }
                DEPLOY_DATABASE=$2; shift ;;
            --no-create-db) NO_CREATE_DB=true ;;
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

    log "reminder: back up the data-protection-keys folder after the first app start (Docs/14 M12) — losing it forces re-entering encrypted server configs"
    log "OK — stage $STAGE finished (log: $LOG_FILE)"
}

main "$@"
