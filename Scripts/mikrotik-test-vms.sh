#!/usr/bin/env bash
# Photon-Bypass local MikroTik test-VM factory (Docs/23-xui-local-test-vm.md, plan 1795201262370 Part 2).
#
# Builds the two local RouterOS test VMs used by Backend/Tests (MikrotikInitializer /
# SyncUserActiveInactiveRemoveTest / ProgramLevelInitializer scenario suite):
#
#   Mikrotik-Base      192.168.56.11  host-only vboxnet0  snapshot clean-test-state
#   Mikrotik-Scenario  192.168.56.12  clone of Base       snapshot clean-test-state
#
# Stages:
#   setup                 build Base + Scenario, import the reference exports,
#                         verify, poweroff and take the clean-test-state snapshots
#   reset [base|scenario|all]   poweroff -> snapshot restore -> start headless -> SSH poll
#   start|stop [base|scenario|all]
#   status                registration / state / snapshot summary
#   clean                 unregister + delete both VMs (confirmation required)
#
# Configuration (environment variables, all optional):
#   MIKROTIK_BASE_NAME / MIKROTIK_SCENARIO_NAME   VM names
#   MIKROTIK_BASE_IP   / MIKROTIK_SCENARIO_IP     host-only addresses
#   MIKROTIK_BOOTSTRAP_IP                          temporary IP used while the
#                                                 fresh router has no config yet
#                                                 (default 192.168.56.250; must be
#                                                 inside the host-only subnet)
#   MIKROTIK_ADMIN_PASSWORD                        admin password set during bootstrap
#                                                 (default: admin)
#   MIKROTIK_SSH_USER / MIKROTIK_SSH_PASSWORD      credentials AFTER the export has
#                                                 been imported — the export file is
#                                                 the single source of config truth,
#                                                 so it may redefine the admin
#                                                 credentials (default: admin/admin)
#   MIKROTIK_BASE_EXPORT        (required for setup) path to `/export show-sensitive`
#                                of the current Base VM (default: the committed test
#                                fixture Scripts/mikrotik-base.rsc — confirmed test
#                                values, same convention as Backend/Tests credentials)
#   MIKROTIK_SCENARIO_EXPORT    (optional) separate export for Scenario; when absent,
#                                Scenario is cloned from Base and only its IP changes
#   ROUTEROS_VERSION            pinned RouterOS release (default below). The image
#                               is the official CHR (Cloud Hosted Router) raw disk
#                               from download.mikrotik.com, converted to VDI with
#                               qemu-img. The free CHR license is permanent; its
#                               1Mbit/s cap only limits forwarded traffic, which the
#                               control-plane test suite never touches.
#   MIKROTIK_IMAGE_PATH         local copy of the CHR .img.zip (or extracted .img /
#                               ready .vdi) instead of downloading
#   MIKROTIK_IMAGE_SHA256       optional sha256 of the downloaded image (verified when set)
#   VBOXMANAGE                  VBoxManage path (default: VBoxManage)
#
# Network notes:
#   - Downloads honor https_proxy / http_proxy (curl does this natively).
#   - The host-only network 192.168.56.1/24 (vboxnet0 or equivalent) is created in
#     preflight when missing.
#   - The fresh router is bootstrapped over the serial console (uart1 -> unix socket),
#     because a fresh RouterOS disk has no IP and an empty admin password. If the
#     serial bootstrap fails, the manual fallback is documented under
#     "SERIAL CONSOLE FALLBACK" in the usage text below.
#
# Serial console fallback (manual, always available):
#   1. MIKROTIK_BASE_EXPORT=... Scripts/mikrotik-test-vms.sh clean   (if needed)
#   2. Start the VM with a GUI window instead of headless:
#        VBoxManage startvm "Mikrotik-Base" --type gui
#   3. In the window log in as admin (empty password) and type the three bootstrap
#      commands yourself (replace the values with yours):
#        /user set admin password="admin"
#        /ip address add address=192.168.56.250/24 interface=ether1
#        /ip service enable ssh,api
#   4. Re-run setup — it will abort (VM exists). Instead continue manually from the
#      import step: copy the export with scp and run /import on the router, or delete
#      the VM and retry the automated setup.
#
# Secrets hygiene: the export files are confirmed test values (owner decision —
# the repo already carries test credentials in Backend/Tests). They are still
# never echoed into the log file: import output is kept in the cache dir, outside
# the repo. The bootstrap/SSH passwords travel via the SSHPASS environment
# variable (sshpass -e), never on command lines.
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd -- "$SCRIPT_DIR/.." && pwd)
LOG_DIR="$SCRIPT_DIR/logs"
CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/photon-bypass/mikrotik-test-vms"

# --- defaults ---------------------------------------------------------------------

MIKROTIK_BASE_NAME=${MIKROTIK_BASE_NAME:-Mikrotik-Base}
MIKROTIK_SCENARIO_NAME=${MIKROTIK_SCENARIO_NAME:-Mikrotik-Scenario}
MIKROTIK_BASE_IP=${MIKROTIK_BASE_IP:-192.168.56.11}
MIKROTIK_SCENARIO_IP=${MIKROTIK_SCENARIO_IP:-192.168.56.12}
MIKROTIK_BOOTSTRAP_IP=${MIKROTIK_BOOTSTRAP_IP:-192.168.56.250}
MIKROTIK_ADMIN_PASSWORD=${MIKROTIK_ADMIN_PASSWORD:-admin}
MIKROTIK_SSH_USER=${MIKROTIK_SSH_USER:-admin}
MIKROTIK_SSH_PASSWORD=${MIKROTIK_SSH_PASSWORD:-admin}
MIKROTIK_BASE_EXPORT=${MIKROTIK_BASE_EXPORT:-$SCRIPT_DIR/mikrotik-base.rsc}
MIKROTIK_SCENARIO_EXPORT=${MIKROTIK_SCENARIO_EXPORT:-$SCRIPT_DIR/mikrotik-scenario.rsc}
HOSTONLY_SUBNET_IP=${HOSTONLY_SUBNET_IP:-192.168.56.1}
HOSTONLY_NETMASK=${HOSTONLY_NETMASK:-255.255.255.0}
HOSTONLY_CIDR=${HOSTONLY_CIDR:-192.168.56.}
ROUTEROS_VERSION=${ROUTEROS_VERSION:-7.20.6}
ROUTEROS_URL=${ROUTEROS_URL:-"https://download.mikrotik.com/routeros/$ROUTEROS_VERSION/chr-$ROUTEROS_VERSION.img.zip"}
MIKROTIK_IMAGE_PATH=${MIKROTIK_IMAGE_PATH:-}
MIKROTIK_IMAGE_SHA256=${MIKROTIK_IMAGE_SHA256:-}
VBOXMANAGE=${VBOXMANAGE:-VBoxManage}
VM_CPUS=${VM_CPUS:-2}
VM_MEMORY_MB=${VM_MEMORY_MB:-512}
SNAPSHOT=${SNAPSHOT:-clean-test-state}
API_PORT=${API_PORT:-8728}

# serial bootstrap timings (seconds)
SERIAL_LOGIN_WAIT=${SERIAL_LOGIN_WAIT:-60}
SERIAL_STEP_WAIT=${SERIAL_STEP_WAIT:-3}
SERIAL_TOTAL_TIMEOUT=${SERIAL_TOTAL_TIMEOUT:-240}

SSH_TIMEOUT_SECONDS=${SSH_TIMEOUT_SECONDS:-120}

STAGE=""
SCOPE=""
ASSUME_YES=false
FAILED=false
LOG_FILE=""

log() { printf '%s\n' "$*" | tee -a "$LOG_FILE"; }

# like log(), but for messages emitted inside functions whose stdout is captured
# by the caller (command substitution) — must never leak into the return value
note() { printf '%s\n' "$*" >>"$LOG_FILE"; }

die() {
    FAILED=true
    printf 'FAIL: %s\n' "$*" | tee -a "$LOG_FILE" >&2
    exit 1
}

usage() {
    awk 'NR > 1 && /^set -euo/ { exit } NR > 1 { sub(/^# ?/, ""); print }' "$0"
}

# --- tooling ----------------------------------------------------------------------

vbox() {
    local args="$*"
    if ! "$VBOXMANAGE" "$@" 2>&1 | tee -a "$LOG_FILE"; then
        die "VBoxManage failed: $args"
    fi
}

vbox_quiet() {
    "$VBOXMANAGE" "$@" 2>>"$LOG_FILE"
}

ssh_opts=(-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ConnectTimeout=10)

ssh_run() { # $1=ip, $2=password, rest=remote command
    local ip=$1 password=$2
    shift 2
    SSHPASS="$password" sshpass -e ssh "${ssh_opts[@]}" "$MIKROTIK_SSH_USER@$ip" "$@"
}

scp_to() { # $1=ip, $2=password, $3=local file
    local ip=$1 password=$2 file=$3
    SSHPASS="$password" sshpass -e scp "${ssh_opts[@]}" "$file" "$MIKROTIK_SSH_USER@$ip:"
}

require_tools() {
    command -v "$VBOXMANAGE" >/dev/null 2>&1 || die "VBoxManage not found (env VBOXMANAGE can point at it)"
    command -v sshpass >/dev/null 2>&1 || die "sshpass not found — install it first (Debian/Ubuntu: apt-get install sshpass)"
    command -v ssh >/dev/null 2>&1 || die "ssh not found"
    command -v scp >/dev/null 2>&1 || die "scp not found"
    command -v qemu-img >/dev/null 2>&1 || die "qemu-img not found (Debian/Ubuntu: apt-get install qemu-utils)"
    if command -v socat >/dev/null 2>&1; then
        # shellcheck disable=SC2317
        serial_connect() { timeout "$1" socat - "UNIX-CONNECT:$2"; }
    elif nc -U /dev/null >/dev/null 2>&1 </dev/null; then
        # shellcheck disable=SC2317
        serial_connect() { timeout "$1" nc -U "$2"; }
    else
        die "neither socat nor 'nc -U' (netcat-openbsd) found — one of them is required for the serial bootstrap"
    fi
}

# --- VirtualBox helpers -----------------------------------------------------------

vm_exists() {
    vbox_quiet list vms | awk -F'"' -v name="$1" '$2 == name { found = 1 } END { exit !found }'
}

vm_running() {
    vbox_quiet list runningvms | awk -F'"' -v name="$1" '$2 == name { found = 1 } END { exit !found }'
}

abort_if_registered() {
    if vm_exists "$1"; then
        die "a VM named '$1' is already registered — refusing to touch it. If it is a leftover from a failed run, remove it with: $0 clean (or VBoxManage unregistervm '$1' --delete)"
    fi
}

poweroff_vm() {
    if vm_running "$1"; then
        vbox controlvm "$1" poweroff
        sleep 2
    else
        log "vm $1 is not running"
    fi
}

ensure_hostonly_network() { # prints the interface name
    local existing
    existing=$(vbox_quiet list hostonlyifs | awk -v ip="$HOSTONLY_SUBNET_IP" '
        /^Name:/            { name = $2 }
        /^IPAddress:/       { if ($2 == ip) print name }')
    if [ -n "$existing" ]; then
        note "host-only network found: $existing ($HOSTONLY_SUBNET_IP)"
        printf '%s\n' "$existing"
        return
    fi

    note "creating host-only network $HOSTONLY_SUBNET_IP/$HOSTONLY_NETMASK"
    local created
    created=$(vbox_quiet hostonlyif create 2>>"$LOG_FILE" | sed -n "s/.*'\(vboxnet[0-9]*\).*/\1/p")
    [ -n "$created" ] || die "could not create a host-only interface"
    "$VBOXMANAGE" hostonlyif ipconfig "$created" --ip "$HOSTONLY_SUBNET_IP" --netmask "$HOSTONLY_NETMASK" >>"$LOG_FILE" 2>&1 \
        || die "hostonlyif ipconfig failed for $created"
    printf '%s\n' "$created"
}

# --- image ------------------------------------------------------------------------

pristine_vdi() { # ensures the pristine CHR disk exists as VDI in the cache; prints its path
    local image_zip="$CACHE_DIR/chr-$ROUTEROS_VERSION.img.zip"
    local image_raw="$CACHE_DIR/chr-$ROUTEROS_VERSION.img"
    local image_vdi="$CACHE_DIR/chr-$ROUTEROS_VERSION.vdi"

    if [ -f "$image_vdi" ]; then
        printf '%s\n' "$image_vdi"
        return
    fi

    if [ -n "$MIKROTIK_IMAGE_PATH" ]; then
        case "$MIKROTIK_IMAGE_PATH" in
            *.vdi)
                [ -f "$MIKROTIK_IMAGE_PATH" ] || die "MIKROTIK_IMAGE_PATH not found: $MIKROTIK_IMAGE_PATH"
                printf '%s\n' "$MIKROTIK_IMAGE_PATH"
                return ;;
            *.img)
                [ -f "$MIKROTIK_IMAGE_PATH" ] || die "MIKROTIK_IMAGE_PATH not found: $MIKROTIK_IMAGE_PATH"
                image_raw="$MIKROTIK_IMAGE_PATH" ;;
            *) image_zip="$MIKROTIK_IMAGE_PATH" ;;
        esac
    fi

    if [ ! -f "$image_raw" ]; then
        [ -f "$image_zip" ] || {
            note "downloading RouterOS CHR $ROUTEROS_VERSION (honors https_proxy)"
            command -v curl >/dev/null 2>&1 || die "curl not found — or set MIKROTIK_IMAGE_PATH to a local copy"
            curl -fsSL --retry 3 --output "$image_zip.part" "$ROUTEROS_URL" || die "download failed: $ROUTEROS_URL"
            mv "$image_zip.part" "$image_zip"
        }

        if [ -n "$MIKROTIK_IMAGE_SHA256" ]; then
            command -v sha256sum >/dev/null 2>&1 || die "sha256sum not found (required for MIKROTIK_IMAGE_SHA256)"
            printf '%s  %s\n' "$MIKROTIK_IMAGE_SHA256" "$image_zip" | sha256sum -c - || die "image checksum mismatch: $image_zip"
        fi

        command -v unzip >/dev/null 2>&1 || die "unzip not found — or extract the .img and set MIKROTIK_IMAGE_PATH=<file>.img"
        unzip -o -j "$image_zip" '*.img' -d "$CACHE_DIR" >/dev/null
        local extracted
        extracted=$(find "$CACHE_DIR" -maxdepth 1 -name '*.img' | head -n 1)
        [ -n "$extracted" ] || die "no .img inside $image_zip — extract it and set MIKROTIK_IMAGE_PATH=<file>.img"
        if [ "$extracted" != "$image_raw" ]; then
            mv "$extracted" "$image_raw"
        fi
    fi

    note "converting CHR raw disk to VDI"
    qemu-img convert -f raw -O vdi "$image_raw" "$image_vdi"

    printf '%s\n' "$image_vdi"
}

# --- serial bootstrap -------------------------------------------------------------

serial_bootstrap() { # $1=vm-name, $2=bootstrap-ip — types the 3 bootstrap commands
    local vm=$1 ip=$2
    local socket="$CACHE_DIR/$vm.serial"
    local serial_log="$CACHE_DIR/$vm.serial.log"

    vbox modifyvm "$vm" --uart1 0x3F8 4 --uartmode1 server "$socket"
    vbox startvm "$vm" --type headless

    log "typing bootstrap over serial console (log: $serial_log — kept out of the repo; it echoes the bootstrap password)"
    {
        sleep "$SERIAL_LOGIN_WAIT"
        printf '\r'
        sleep "$SERIAL_STEP_WAIT"
        printf 'admin\r'
        sleep "$SERIAL_STEP_WAIT"
        printf '\r'
        sleep "$SERIAL_STEP_WAIT"
        printf '/user set admin password="%s"\r' "$MIKROTIK_ADMIN_PASSWORD"
        sleep "$SERIAL_STEP_WAIT"
        printf '/ip address add address=%s/24 interface=ether1\r' "$ip"
        sleep "$SERIAL_STEP_WAIT"
        printf '/ip service enable ssh,api\r'
        sleep "$SERIAL_STEP_WAIT"
        printf '/ip service set ssh,api disabled=no\r'
        sleep "$SERIAL_STEP_WAIT"
        printf 'quit\r'
        sleep "$SERIAL_STEP_WAIT"
    } | serial_connect "$SERIAL_TOTAL_TIMEOUT" "$socket" >"$serial_log" 2>&1 || true

    # Authoritative check is SSH below; the serial log is only for debugging.
    if ! wait_ssh "$vm" "$ip" "$MIKROTIK_ADMIN_PASSWORD" 60; then
        die "serial bootstrap did not produce a reachable router at $ip — see $serial_log, or use the manual GUI fallback documented in '$0 --help'"
    fi
}

# --- SSH helpers ------------------------------------------------------------------

wait_ssh() { # $1=vm, $2=ip, $3=password, $4=timeout-seconds
    local vm=$1 ip=$2 password=$3 timeout_s=$4
    local start=$(( $(date +%s) ))
    log "waiting for SSH on $MIKROTIK_SSH_USER@$ip (vm: $vm, timeout: ${timeout_s}s)"
    while [ $(( $(date +%s) - start )) -lt "$timeout_s" ]; do
        if ssh_run "$ip" "$password" ':put "ok"' >/dev/null 2>&1; then
            log "SSH is up on $ip"
            return 0
        fi
        sleep 3
    done
    return 1
}

api_port_open() {
    timeout 3 bash -c "</dev/tcp/$1/$2" 2>/dev/null
}

user_manager_is_empty() { # $1=ip, $2=password
    local output
    output=$(ssh_run "$1" "$2" '/user-manager user print') || return 2
    # header/comment lines start with '#' or 'Flags:' — any other non-empty line is a user
    if printf '%s\n' "$output" | sed '/^[[:space:]]*#/d; /^[[:space:]]*Flags:/d' | grep -q '[^[:space:]]'; then
        return 1
    fi
    return 0
}

import_export() { # $1=vm, $2=export-file, $3=current-ip, $4=current-password, $5=final-ip
    local vm=$1 export_file=$2 ip=$3 password=$4 final_ip=$5
    local remote_name="import-$vm.rsc"
    local import_log="$CACHE_DIR/import-$vm.log"

    [ -f "$export_file" ] || die "export file not found: $export_file"

    log "copying export to $vm ($ip)"
    scp_to "$ip" "$password" "$export_file" || die "scp of the export failed"

    log "importing export on $vm"
    local output
    output=$(ssh_run "$ip" "$password" "/import file=$remote_name") || {
        printf '%s\n' "$output" >"$import_log"
        die "import failed on $vm — full output kept out of the repo at $import_log"
    }
    printf '%s\n' "$output" >"$import_log"

    if ! printf '%s\n' "$output" | grep -qi 'Script file loaded and executed successfully'; then
        die "import did not report success on $vm — full output kept out of the repo at $import_log"
    fi
    log "import ok on $vm"

    # users are per-test state, not config: strip whatever the reference carried so
    # the clean-test-state snapshot starts with an empty user-manager (profiles and
    # limitations stay — tests create/remove their own users on top)
    ssh_run "$ip" "$password" "/user-manager user-profile remove [find]; /user-manager user remove [find]" \
        >/dev/null 2>&1 || die "could not clear user-manager users on $vm after the import"
    log "user-manager users cleared on $vm"

    ensure_address "$ip" "$password" "$final_ip"
}

ensure_address() { # $1=current-ip, $2=password, $3=final-ip — only 192.168.56.* is managed
    local ip=$1 password=$2 final_ip=$3
    log "ensuring address $final_ip/24 on ether1 (removing other $HOSTONLY_CIDR* addresses)"
    ssh_run "$ip" "$password" "/ip address add address=$final_ip/24 interface=ether1" >/dev/null 2>&1 || true
    # removing the address the session rides on drops the connection — expected
    ssh_run "$ip" "$password" \
        "/ip address remove [find address~\"$HOSTONLY_CIDR\" and address!=\"$final_ip/24\"]" >/dev/null 2>&1 || true
}

verify_vm() { # $1=vm, $2=ip
    local vm=$1 ip=$2
    wait_ssh "$vm" "$ip" "$MIKROTIK_SSH_PASSWORD" "$SSH_TIMEOUT_SECONDS" \
        || die "SSH not reachable on $ip with MIKROTIK_SSH_USER/MIKROTIK_SSH_PASSWORD (the export defines the final credentials) — vm: $vm"
    api_port_open "$ip" "$API_PORT" || die "api port $API_PORT is not open on $ip — vm: $vm"
    log "api port $API_PORT open on $ip"
    user_manager_is_empty "$ip" "$MIKROTIK_SSH_PASSWORD" \
        || die "/user-manager user print is not empty on $vm — the export must ship without users (tests create/remove their own)"
    log "user-manager is empty on $vm"
}

print_config_snippet() {
    cat <<EOF

OK — test VMs ready. TestVmOptions snippet for Backend/Tests/appsettings.json:

  "TestVmOptions": {
    "$MIKROTIK_BASE_NAME":     { "Ip": "$MIKROTIK_BASE_IP", "Username": "$MIKROTIK_SSH_USER", "Password": "$MIKROTIK_SSH_PASSWORD", "ApiPort": $API_PORT },
    "$MIKROTIK_SCENARIO_NAME": { "Ip": "$MIKROTIK_SCENARIO_IP", "Username": "$MIKROTIK_SSH_USER", "Password": "$MIKROTIK_SSH_PASSWORD", "ApiPort": $API_PORT }
  }

Machine overrides without code changes (also work for dotnet test):
  TestVmOptions__${MIKROTIK_BASE_NAME}__Ip=$MIKROTIK_BASE_IP
  TestVmOptions__${MIKROTIK_SCENARIO_NAME}__Ip=$MIKROTIK_SCENARIO_IP

Snapshot semantics for both VMs: '$SNAPSHOT' (tests restore it per run via MikrotikInitializer).
EOF
}

# --- stages -----------------------------------------------------------------------

setup_vm_base() {
    local hostonly_if disk
    hostonly_if=$(ensure_hostonly_network)

    log "creating VM $MIKROTIK_BASE_NAME ($MIKROTIK_BASE_IP)"
    disk="$CACHE_DIR/$MIKROTIK_BASE_NAME-disk.vdi"
    vbox clonemedium disk "$(pristine_vdi)" "$disk" --format VDI
    vbox createvm --name "$MIKROTIK_BASE_NAME" --ostype Other_64 --register
    vbox modifyvm "$MIKROTIK_BASE_NAME" --cpus "$VM_CPUS" --memory "$VM_MEMORY_MB" --ioapic on \
        --nic1 hostonly --hostonlyadapter1 "$hostonly_if" --nictype1 82540EM
    vbox storagectl "$MIKROTIK_BASE_NAME" --name SATA --add sata --bootable on
    vbox storageattach "$MIKROTIK_BASE_NAME" --storagectl SATA --port 0 --device 0 --type hdd --medium "$disk"

    serial_bootstrap "$MIKROTIK_BASE_NAME" "$MIKROTIK_BOOTSTRAP_IP"
    import_export "$MIKROTIK_BASE_NAME" "$MIKROTIK_BASE_EXPORT" "$MIKROTIK_BOOTSTRAP_IP" "$MIKROTIK_ADMIN_PASSWORD" "$MIKROTIK_BASE_IP"
    verify_vm "$MIKROTIK_BASE_NAME" "$MIKROTIK_BASE_IP"

    poweroff_vm "$MIKROTIK_BASE_NAME"
    vbox snapshot "$MIKROTIK_BASE_NAME" take "$SNAPSHOT"
    log "snapshot '$SNAPSHOT' taken for $MIKROTIK_BASE_NAME"
}

setup_vm_scenario() {
    vm_exists "$MIKROTIK_BASE_NAME" || die "$MIKROTIK_BASE_NAME must be set up first"

    log "cloning $MIKROTIK_BASE_NAME -> $MIKROTIK_SCENARIO_NAME"
    vbox clonevm "$MIKROTIK_BASE_NAME" --name "$MIKROTIK_SCENARIO_NAME" --register
    vbox startvm "$MIKROTIK_SCENARIO_NAME" --type headless

    if [ -n "$MIKROTIK_SCENARIO_EXPORT" ]; then
        sleep 10
        wait_ssh "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_BASE_IP" "$MIKROTIK_SSH_PASSWORD" "$SSH_TIMEOUT_SECONDS" \
            || die "cloned scenario VM did not come up on $MIKROTIK_BASE_IP"
        import_export "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_SCENARIO_EXPORT" "$MIKROTIK_BASE_IP" "$MIKROTIK_SSH_PASSWORD" "$MIKROTIK_SCENARIO_IP"
    else
        sleep 10
        wait_ssh "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_BASE_IP" "$MIKROTIK_SSH_PASSWORD" "$SSH_TIMEOUT_SECONDS" \
            || die "cloned scenario VM did not come up on $MIKROTIK_BASE_IP"
        ensure_address "$MIKROTIK_BASE_IP" "$MIKROTIK_SSH_PASSWORD" "$MIKROTIK_SCENARIO_IP"
    fi

    verify_vm "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_SCENARIO_IP"

    poweroff_vm "$MIKROTIK_SCENARIO_NAME"
    vbox snapshot "$MIKROTIK_SCENARIO_NAME" take "$SNAPSHOT"
    log "snapshot '$SNAPSHOT' taken for $MIKROTIK_SCENARIO_NAME"
}

run_stage_setup() {
    [ -n "$MIKROTIK_BASE_EXPORT" ] || die "MIKROTIK_BASE_EXPORT is required for setup — path to a '/export show-sensitive' of the current Base VM, stored outside the repo"

    require_tools
    abort_if_registered "$MIKROTIK_BASE_NAME"
    abort_if_registered "$MIKROTIK_SCENARIO_NAME"

    mkdir -p "$CACHE_DIR"
    setup_vm_base
    setup_vm_scenario
    print_config_snippet
}

scope_vms() {
    case "${SCOPE:-all}" in
        base) printf '%s\n' "$MIKROTIK_BASE_NAME" ;;
        scenario) printf '%s\n' "$MIKROTIK_SCENARIO_NAME" ;;
        all) printf '%s\n%s\n' "$MIKROTIK_BASE_NAME" "$MIKROTIK_SCENARIO_NAME" ;;
        *) die "unknown scope '$SCOPE' — use base|scenario|all" ;;
    esac
}

run_stage_reset() {
    require_tools
    local vm
    while IFS= read -r vm; do
        vm_exists "$vm" || die "VM '$vm' is not registered"
        log "resetting $vm: poweroff -> snapshot '$SNAPSHOT' -> start headless"
        poweroff_vm "$vm"
        vbox snapshot "$vm" restore "$SNAPSHOT"
        vbox startvm "$vm" --type headless
        local ip="$MIKROTIK_BASE_IP"
        if [ "$vm" = "$MIKROTIK_SCENARIO_NAME" ]; then
            ip="$MIKROTIK_SCENARIO_IP"
        fi
        wait_ssh "$vm" "$ip" "$MIKROTIK_SSH_PASSWORD" "$SSH_TIMEOUT_SECONDS" \
            || die "$vm did not become SSH-reachable on $ip after the snapshot restore"
    done < <(scope_vms)
    log "reset finished"
}

run_stage_start() {
    local vm
    while IFS= read -r vm; do
        vm_exists "$vm" || die "VM '$vm' is not registered"
        if vm_running "$vm"; then
            log "$vm is already running"
            continue
        fi
        vbox startvm "$vm" --type headless
    done < <(scope_vms)
}

run_stage_stop() {
    local vm
    while IFS= read -r vm; do
        vm_exists "$vm" || die "VM '$vm' is not registered"
        poweroff_vm "$vm"
    done < <(scope_vms)
}

run_stage_status() {
    local vm ip
    while IFS= read -r vm; do
        if ! vm_exists "$vm"; then
            log "$vm: not registered"
            continue
        fi
        ip="$MIKROTIK_BASE_IP"
        if [ "$vm" = "$MIKROTIK_SCENARIO_NAME" ]; then
            ip="$MIKROTIK_SCENARIO_IP"
        fi
        local state=stopped
        if vm_running "$vm"; then
            state=running
        fi
        local snapshots
        snapshots=$(vbox_quiet snapshot "$vm" list --machinereadable 2>/dev/null | grep -c "^SnapshotName=" || true)
        log "$vm: $state, expected-ip: $ip, snapshots: $snapshots"
    done < <(scope_vms)
}

run_stage_clean() {
    [ "$ASSUME_YES" = true ] || {
        [ -t 0 ] || die "stdin is not a terminal — pass --yes for non-interactive clean"
        printf 'Delete the following VMs (all disks, all snapshots)?\n'
        scope_vms | sed 's/^/  - /'
        printf 'This cannot be undone. Type the full name of the first VM to confirm: '
        local answer
        read -r answer
        [ "$answer" = "$MIKROTIK_BASE_NAME" ] || die "aborted by user"
    }
    local vm
    while IFS= read -r vm; do
        if vm_exists "$vm"; then
            poweroff_vm "$vm"
            vbox unregistervm "$vm" --delete
            log "deleted $vm"
        else
            log "$vm: not registered"
        fi
    done < <(scope_vms)
}

# --- argument parsing ---------------------------------------------------------------

parse_args() {
    while [ $# -gt 0 ]; do
        case "$1" in
            setup | reset | start | stop | status | clean)
                [ -z "$STAGE" ] || { printf 'error: multiple stages\n' >&2; exit 1; }
                STAGE=$1 ;;
            base | scenario | all)
                [ -z "$SCOPE" ] || { printf 'error: multiple scopes\n' >&2; exit 1; }
                SCOPE=$1 ;;
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
    [ -n "$STAGE" ] || { usage >&2; exit 1; }

    mkdir -p "$LOG_DIR" "$CACHE_DIR"
    LOG_FILE="$LOG_DIR/mikrotik-test-vms-$(date +%Y%m%d-%H%M%S).log"

    trap 'status=$?; if [ "$status" -ne 0 ] && [ "$FAILED" = false ]; then
            printf "FAIL: unexpected error (exit %s, log: %s)\n" "$status" "$LOG_FILE" >&2
        fi' EXIT

    log "photon-bypass mikrotik test VMs — stage: $STAGE, started: $(date '+%Y-%m-%d %H:%M:%S')"

    case "$STAGE" in
        setup) run_stage_setup ;;
        reset) run_stage_reset ;;
        start) run_stage_start ;;
        stop) run_stage_stop ;;
        status) run_stage_status ;;
        clean) run_stage_clean ;;
    esac

    log "OK — stage $STAGE finished (log: $LOG_FILE)"
}

main "$@"
