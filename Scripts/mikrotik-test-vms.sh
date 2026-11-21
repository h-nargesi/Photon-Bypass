#!/usr/bin/env bash
# Photon-Bypass local MikroTik test-VM factory (Docs/23-xui-local-test-vm.md, plan 1795201262370 Part 2).
#
# Builds the two local RouterOS test VMs used by Backend/Tests (MikrotikInitializer /
# SyncUserActiveInactiveRemoveTest / ProgramLevelInitializer scenario suite):
#
#   Mikrotik-Base      192.168.56.11  host-only vboxnet0  snapshot clean-test-state
#   Mikrotik-Scenario  192.168.56.12  own build + own export fixture
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
#   MIKROTIK_SCENARIO_EXPORT    export fixture for Scenario (default: the committed
#                               Scripts/mikrotik-scenario.rsc; built from the same
#                               pristine CHR image as Base — NOT a clone: a cloned
#                               NIC gets a new MAC and RouterOS renames ether1 to
#                               ether2, invalidating every imported binding)
#   ROUTEROS_VERSION            pinned RouterOS release (default below). The image
#                               is the official CHR (Cloud Hosted Router) raw disk
#                               from download.mikrotik.com, converted to VDI with
#                               qemu-img. The free CHR license is permanent; its
#                               1Mbit/s cap only limits forwarded traffic, which the
#                               control-plane test suite never touches.
#   MIKROTIK_IMAGE_PATH         local copy of the CHR .img.zip (or extracted .img /
#                               ready .vdi) instead of downloading
#   MIKROTIK_IMAGE_SHA256       optional sha256 of the downloaded image (verified when set)
#   ROUTEROS_EXTRA_URL          pinned extra-packages archive that carries the
#                               user-manager package (CHR images do not bundle it)
#   MIKROTIK_EXTRA_PATH         local copy of the extra-packages .zip instead of
#                               downloading
#   MIKROTIK_INSTALL_USERMANAGER  set to false only when the export fixtures do
#                               not use /user-manager (default true)
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
# Windows hosts (running from WSL against the Windows VirtualBox):
#   - Set/keep VBOXMANAGE=VBoxManage.exe (auto-selected when the Linux VBoxManage is
#     absent but VBoxManage.exe is resolvable through WSL interop).
#   - Put the cache on a Windows-visible path, e.g. XDG_CACHE_HOME=/mnt/d/.cache
#     (WSL-only paths are not reachable by the Windows VirtualBox).
#   - Path arguments given to VBoxManage are translated with wslpath automatically.
#   - The serial bootstrap uses VBox tcpserver mode (SERIAL_TCP_PORT, default 4771):
#     socat connects to the Windows host — the WSL default gateway — instead of a
#     local UNIX socket. Named-pipe mode is not reachable from WSL.
#
# Serial console fallback (manual, always available):
#   1. MIKROTIK_BASE_EXPORT=... Scripts/mikrotik-test-vms.sh clean   (if needed)
#   2. Start the VM with a GUI window instead of headless:
#        VBoxManage startvm "Mikrotik-Base" --type gui
#   3. In the window log in as admin (empty password), accept the license default,
#      answer the forced first-login password change (enter the admin password
#      twice) and type the two bootstrap commands yourself (replace the values):
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
ROUTEROS_EXTRA_URL=${ROUTEROS_EXTRA_URL:-"https://download.mikrotik.com/routeros/$ROUTEROS_VERSION/all_packages-x86-$ROUTEROS_VERSION.zip"}
MIKROTIK_IMAGE_PATH=${MIKROTIK_IMAGE_PATH:-}
MIKROTIK_IMAGE_SHA256=${MIKROTIK_IMAGE_SHA256:-}
MIKROTIK_EXTRA_PATH=${MIKROTIK_EXTRA_PATH:-}
MIKROTIK_INSTALL_USERMANAGER=${MIKROTIK_INSTALL_USERMANAGER:-true}
VBOXMANAGE=${VBOXMANAGE:-VBoxManage}
VM_CPUS=${VM_CPUS:-2}
VM_MEMORY_MB=${VM_MEMORY_MB:-512}
SNAPSHOT=${SNAPSHOT:-clean-test-state}
API_PORT=${API_PORT:-8728}

# serial bootstrap: the first-login dialog is keyed — the driver answers each
# observed prompt (login, license, forced password change, command prompt) —
# with this per-prompt wait in seconds
SERIAL_STEP_TIMEOUT=${SERIAL_STEP_TIMEOUT:-240}
# serial-over-TCP port for Windows hosts (VBox uartmode tcpserver)
SERIAL_TCP_PORT=${SERIAL_TCP_PORT:-4771}

SSH_TIMEOUT_SECONDS=${SSH_TIMEOUT_SECONDS:-120}

# --- Windows-host (WSL) support -----------------------------------------------------

# When running from WSL against the Windows VirtualBox, VBoxManage is the Windows
# executable: file arguments must be Windows paths and serial-over-UNIX-socket does
# not exist (named pipe + PowerShell driver instead). Auto-detected, or forced with
# VBOX_ON_WINDOWS=1/0.
if ! command -v "$VBOXMANAGE" >/dev/null 2>&1 && command -v VBoxManage.exe >/dev/null 2>&1; then
    VBOXMANAGE=VBoxManage.exe
fi
if [ -z "${VBOX_ON_WINDOWS:-}" ]; then
    case "$VBOXMANAGE" in
        *.exe) VBOX_ON_WINDOWS=true ;;
        *)     VBOX_ON_WINDOWS=false ;;
    esac
fi

wp() { # translates a path argument for the Windows VBoxManage (no-op on Linux hosts)
    if [ "$VBOX_ON_WINDOWS" = true ]; then
        wslpath -w "$1"
    else
        printf '%s\n' "$1"
    fi
}

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
    # tr -d '\r': VBoxManage.exe emits CRLF line endings, which would corrupt
    # awk/sed parsing of its output on Windows hosts
    "$VBOXMANAGE" "$@" 2>>"$LOG_FILE" | tr -d '\r'
}

# ServerAlive*: the address-removal step drops the connection the session rides
# on; without keepalives ssh would hang until the TCP timeout
ssh_opts=(-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ConnectTimeout=10 -o ServerAliveInterval=5 -o ServerAliveCountMax=3)

ssh_run() { # $1=ip, $2=password, rest=remote command
    local ip=$1 password=$2
    shift 2
    SSHPASS="$password" sshpass -e ssh "${ssh_opts[@]}" "$MIKROTIK_SSH_USER@$ip" "$@"
}

scp_to() { # $1=ip, $2=password, $3=local file, $4=remote name
    local ip=$1 password=$2 file=$3 remote=$4
    SSHPASS="$password" sshpass -e scp "${ssh_opts[@]}" "$file" "$MIKROTIK_SSH_USER@$ip:$remote"
}

require_tools() {
    command -v "$VBOXMANAGE" >/dev/null 2>&1 || die "VBoxManage not found (env VBOXMANAGE can point at it)"
    command -v sshpass >/dev/null 2>&1 || die "sshpass not found — install it first (Debian/Ubuntu: apt-get install sshpass)"
    command -v ssh >/dev/null 2>&1 || die "ssh not found"
    command -v scp >/dev/null 2>&1 || die "scp not found"
    command -v qemu-img >/dev/null 2>&1 || die "qemu-img not found (Debian/Ubuntu: apt-get install qemu-utils)"
    command -v python3 >/dev/null 2>&1 || die "python3 not found — required for the keyed serial bootstrap"
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
    # Windows host-only names contain spaces ("VirtualBox Host-Only Ethernet
    # Adapter"), so capture the whole value, not just the second field
    existing=$(vbox_quiet list hostonlyifs | awk -v ip="$HOSTONLY_SUBNET_IP" '
        /^Name:/            { name = $0; sub(/^Name:[[:space:]]*/, "", name) }
        /^IPAddress:/       { if ($2 == ip) print name }')
    if [ -n "$existing" ]; then
        note "host-only network found: $existing ($HOSTONLY_SUBNET_IP)"
        printf '%s\n' "$existing"
        return
    fi

    note "creating host-only network $HOSTONLY_SUBNET_IP/$HOSTONLY_NETMASK"
    local created
    created=$(vbox_quiet hostonlyif create 2>>"$LOG_FILE" | sed -n "s/.*'\([^']*\)'.*/\1/p")
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

# --- extra packages -----------------------------------------------------------------

usermanager_npk() { # prints the path of the user-manager package for ROUTEROS_VERSION
    local npk="$CACHE_DIR/user-manager-$ROUTEROS_VERSION.npk"

    if [ -f "$npk" ]; then
        printf '%s\n' "$npk"
        return
    fi

    local zip="$CACHE_DIR/all_packages-x86-$ROUTEROS_VERSION.zip"
    if [ -n "$MIKROTIK_EXTRA_PATH" ]; then
        zip="$MIKROTIK_EXTRA_PATH"
        [ -f "$zip" ] || die "MIKROTIK_EXTRA_PATH not found: $zip"
    elif [ ! -f "$zip" ]; then
        note "downloading RouterOS extra packages $ROUTEROS_VERSION (honors https_proxy)"
        command -v curl >/dev/null 2>&1 || die "curl not found — or set MIKROTIK_EXTRA_PATH to a local copy"
        curl -fsSL --retry 3 --output "$zip.part" "$ROUTEROS_EXTRA_URL" || die "download failed: $ROUTEROS_EXTRA_URL"
        mv "$zip.part" "$zip"
    fi

    command -v unzip >/dev/null 2>&1 || die "unzip not found"
    unzip -o -j "$zip" "user-manager-$ROUTEROS_VERSION.npk" -d "$CACHE_DIR" >/dev/null
    [ -f "$npk" ] || die "user-manager-$ROUTEROS_VERSION.npk not inside $zip"
    printf '%s\n' "$npk"
}

install_usermanager() { # $1=vm, $2=ip, $3=password — CHR ships without user-manager
    local vm=$1 ip=$2 password=$3
    local npk npk_name
    npk=$(usermanager_npk)
    npk_name=$(basename "$npk")

    log "installing the user-manager package on $vm (not bundled with CHR images)"
    scp_to "$ip" "$password" "$npk" "$npk_name" || die "scp of the user-manager package failed"

    # RouterOS installs packages placed in the root file store at boot time
    log "rebooting $vm for the package activation"
    printf 'y\n' | SSHPASS="$password" sshpass -e ssh "${ssh_opts[@]}" \
        "$MIKROTIK_SSH_USER@$ip" '/system reboot' >/dev/null 2>&1 || true
    if ! wait_ssh "$vm" "$ip" "$password" 240; then
        die "$vm did not come back after the user-manager install reboot"
    fi
    ssh_run "$ip" "$password" '/user-manager user print' >/dev/null 2>&1 \
        || die "/user-manager is still unavailable on $vm after the package install"
    log "user-manager is active on $vm"
}

# --- serial bootstrap -------------------------------------------------------------

serial_dialog() { # $1=address(tcp:HOST:PORT|unix:PATH) $2=admin-password $3=bootstrap-ip $4=log — keyed first-login conversation
    python3 - "$1" "$2" "$3" "$4" "$SERIAL_STEP_TIMEOUT" <<'PYEOF'
import re
import socket
import sys
import time

address, password, bootstrap_ip, log_path, step_timeout = sys.argv[1:6]
step_timeout = int(step_timeout)

# The console may already sit at a prompt without printing anything on connect;
# an empty line makes RouterOS re-print the current prompt.
WAKE_TIMEOUT = 20

connect_deadline = time.time() + 60
sock = None
while sock is None and time.time() < connect_deadline:
    try:
        if address.startswith('tcp:'):
            host, port = address[4:].rsplit(':', 1)
            sock = socket.create_connection((host, int(port)), timeout=5)
        else:
            sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            sock.connect(address[5:])
    except OSError:
        time.sleep(0.5)
if sock is None:
    sys.exit('could not connect to the serial console: ' + address)
sock.settimeout(1.0)

log = open(log_path, 'wb', 0)  # unbuffered
buf = b''


def pump(seconds):
    global buf
    end = time.time() + seconds
    while time.time() < end:
        try:
            data = sock.recv(4096)
            if data:
                log.write(data)
                buf += data
        except socket.timeout:
            pass
        except OSError:
            sys.exit('serial connection closed unexpectedly')


def visible():
    # strip ANSI/VT control sequences so prompts match as contiguous text
    cleaned = re.sub(rb'\x1b\[[0-9;?]*[a-zA-Z]', b'', buf)
    return re.sub(rb'\x1b[=>]', b'', cleaned)


def send(line):
    sock.sendall(line.encode('ascii') + b'\r')


def wait_for(pattern, timeout_seconds):
    global buf
    rx = re.compile(pattern)
    started = time.time()
    end = started + timeout_seconds
    last_wake = 0.0
    while time.time() < end:
        if rx.search(visible()):
            return True
        pump(1.0)
        # the console can sit at a prompt without printing anything on connect;
        # nudge it with an empty line while waiting for the very first prompt
        if not buf and started + WAKE_TIMEOUT > time.time() > last_wake + 5:
            send('')
            last_wake = time.time()
    return False


# First login on a fresh RouterOS disk: console login with the empty password,
# the software-license question, the forced password-change dialog (new +
# repeat; the dialog consumes it before any command prompt), then the admin
# command prompt for the two bootstrap commands.
STEPS = [
    (rb'MikroTik Login:', 'admin'),
    (rb'Password:', ''),
    (rb'\[Y/n\]:', 'n'),                                  # license question: skip the EULA text
    (rb'new password>', password),
    (rb'repeat new password>', password),
    (rb'\[admin@[^\]]*\] *>', '/ip address add address=%s/24 interface=ether1' % bootstrap_ip),
    (rb'\[admin@[^\]]*\] *>', '/ip service enable ssh,api'),
    (rb'\[admin@[^\]]*\] *>', '/ip service set ssh,api disabled=no'),
    (rb'\[admin@[^\]]*\] *>', 'quit'),
]

for pattern, response in STEPS:
    if not wait_for(pattern, step_timeout):
        sys.exit('serial bootstrap timed out waiting for: %s (last output: %r)'
                 % (pattern.decode('ascii'), visible()[-200:]))
    send(response)
    buf = b''
    pump(1.0)

sock.close()
log.close()
PYEOF
}

serial_bootstrap() { # $1=vm-name, $2=bootstrap-ip — runs the keyed first-login conversation
    local vm=$1 ip=$2
    local serial_log="$CACHE_DIR/$vm.serial.log"
    local address

    if [ "$VBOX_ON_WINDOWS" = true ]; then
        # VBox on Windows offers the serial console as a named pipe or a TCP server;
        # only TCP is reachable from WSL, through the Windows host (the WSL default
        # gateway). The uart is switched off again after the setup (see setup_vm_base)
        # so concurrently running clones cannot collide on the port.
        local host_ip
        host_ip=$(ip route show default | awk '{print $3; exit}')
        [ -n "$host_ip" ] || die "could not determine the WSL default gateway (Windows host) for the serial TCP connection"
        vbox modifyvm "$vm" --uart1 0x3F8 4 --uartmode1 tcpserver "$SERIAL_TCP_PORT"
        vbox startvm "$vm" --type headless
        address="tcp:$host_ip:$SERIAL_TCP_PORT"
    else
        local socket="$CACHE_DIR/$vm.serial"
        vbox modifyvm "$vm" --uart1 0x3F8 4 --uartmode1 server "$socket"
        vbox startvm "$vm" --type headless
        address="unix:$socket"
    fi

    log "typing bootstrap over serial console ($address; log: $serial_log — kept out of the repo; it echoes the bootstrap password)"
    serial_dialog "$address" "$MIKROTIK_ADMIN_PASSWORD" "$ip" "$serial_log" \
        || die "serial bootstrap conversation failed — see $serial_log, or use the manual GUI fallback documented in '$0 --help'"

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
    local copy="$CACHE_DIR/$remote_name"

    [ -f "$export_file" ] || die "export file not found: $export_file"

    # pristine CHR images (and the Base clone) already carry a dhcp-client on
    # ether1; the export re-adds it — drop the pre-existing one so /import is clean
    ssh_run "$ip" "$password" '/ip dhcp-client remove [find]' >/dev/null 2>&1 || true

    # normalize to LF — a CRLF checkout (e.g. core.autocrlf on Windows) would
    # otherwise break /import on the router
    tr -d '\r' <"$export_file" >"$copy"

    log "copying export to $vm ($ip)"
    scp_to "$ip" "$password" "$copy" "$remote_name" || die "scp of the export failed"

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

create_chr_vm() { # $1=vm-name — fresh VM from the pristine CHR disk, host-only NIC
    local vm=$1 hostonly_if disk
    hostonly_if=$(ensure_hostonly_network)

    disk="$CACHE_DIR/$vm-disk.vdi"
    vbox clonemedium disk "$(wp "$(pristine_vdi)")" "$(wp "$disk")" --format VDI
    vbox createvm --name "$vm" --ostype Other_64 --register
    vbox modifyvm "$vm" --cpus "$VM_CPUS" --memory "$VM_MEMORY_MB" --ioapic on \
        --nic1 hostonly --hostonlyadapter1 "$hostonly_if" --nictype1 82540EM
    vbox storagectl "$vm" --name SATA --add sata --bootable on
    vbox storageattach "$vm" --storagectl SATA --port 0 --device 0 --type hdd --medium "$(wp "$disk")"
}

snapshot_clean_state() { # $1=vm-name — poweroff, detach the serial console, snapshot
    local vm=$1
    poweroff_vm "$vm"
    # the scenario build and later concurrent runs must not collide on the serial
    # socket/TCP port — the console is not needed once the bootstrap has succeeded
    vbox modifyvm "$vm" --uart1 off
    vbox snapshot "$vm" take "$SNAPSHOT"
    log "snapshot '$SNAPSHOT' taken for $vm"
}

setup_vm_base() {
    log "creating VM $MIKROTIK_BASE_NAME ($MIKROTIK_BASE_IP)"
    create_chr_vm "$MIKROTIK_BASE_NAME"

    serial_bootstrap "$MIKROTIK_BASE_NAME" "$MIKROTIK_BOOTSTRAP_IP"
    if [ "$MIKROTIK_INSTALL_USERMANAGER" = true ]; then
        install_usermanager "$MIKROTIK_BASE_NAME" "$MIKROTIK_BOOTSTRAP_IP" "$MIKROTIK_ADMIN_PASSWORD"
    fi
    import_export "$MIKROTIK_BASE_NAME" "$MIKROTIK_BASE_EXPORT" "$MIKROTIK_BOOTSTRAP_IP" "$MIKROTIK_ADMIN_PASSWORD" "$MIKROTIK_BASE_IP"
    verify_vm "$MIKROTIK_BASE_NAME" "$MIKROTIK_BASE_IP"

    snapshot_clean_state "$MIKROTIK_BASE_NAME"
}

setup_vm_scenario() {
    # NOT a clone of Base: cloning assigns a new MAC, RouterOS then renames the
    # interface (ether1 -> ether2) and every address/service binding imported on
    # Base turns INVALID. Build from the same pristine disk + own fixture instead.
    [ -f "$MIKROTIK_SCENARIO_EXPORT" ] || die "scenario export not found: $MIKROTIK_SCENARIO_EXPORT (set MIKROTIK_SCENARIO_EXPORT or build Base first)"
    vm_exists "$MIKROTIK_BASE_NAME" || die "$MIKROTIK_BASE_NAME must be set up first"

    log "creating VM $MIKROTIK_SCENARIO_NAME ($MIKROTIK_SCENARIO_IP)"
    create_chr_vm "$MIKROTIK_SCENARIO_NAME"

    serial_bootstrap "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_BOOTSTRAP_IP"
    if [ "$MIKROTIK_INSTALL_USERMANAGER" = true ]; then
        install_usermanager "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_BOOTSTRAP_IP" "$MIKROTIK_ADMIN_PASSWORD"
    fi
    import_export "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_SCENARIO_EXPORT" "$MIKROTIK_BOOTSTRAP_IP" "$MIKROTIK_ADMIN_PASSWORD" "$MIKROTIK_SCENARIO_IP"
    verify_vm "$MIKROTIK_SCENARIO_NAME" "$MIKROTIK_SCENARIO_IP"

    snapshot_clean_state "$MIKROTIK_SCENARIO_NAME"
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
    for vm in $(scope_vms); do
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
    done
    log "reset finished"
}

run_stage_start() {
    local vm
    for vm in $(scope_vms); do
        vm_exists "$vm" || die "VM '$vm' is not registered"
        if vm_running "$vm"; then
            log "$vm is already running"
            continue
        fi
        vbox startvm "$vm" --type headless
    done
}

run_stage_stop() {
    local vm
    for vm in $(scope_vms); do
        vm_exists "$vm" || die "VM '$vm' is not registered"
        poweroff_vm "$vm"
    done
}

run_stage_status() {
    local vm ip
    for vm in $(scope_vms); do
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
    done
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
    for vm in $(scope_vms); do
        if vm_exists "$vm"; then
            poweroff_vm "$vm"
            vbox unregistervm "$vm" --delete
            log "deleted $vm"
        else
            log "$vm: not registered"
        fi
    done
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
