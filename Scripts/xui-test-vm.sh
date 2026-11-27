#!/usr/bin/env bash
# Photon-Bypass local x-ui test-VM factory (Docs/23-xui-local-test-vm.md, plan 1795201262370 Part 1).
#
# Builds the Debian VM that runs the pinned test x-ui panel for future integration
# tests (Docs/23 §4.3 — the config placeholder in Backend/Tests/appsettings.json):
#
#   Xui-Test  192.168.56.13  nic1 NAT + nic2 host-only (virtio)  snapshot clean-test-state
#             panel http://192.168.56.13:2087  (no web base path)
#             test inbound: VLESS+ws, no TLS, port 10000, path /testws
#
# Image: Debian 12 "generic" qcow2 (pinned snapshot + SHA512 verify) turned into
# a VDI, booted once against a NoCloud seed ISO (cloud-init) for zero-GUI setup.
# The "generic" flavor (standard kernel), not "genericcloud" (cloud kernel): the
# cloud kernel latches onto KVM paravirtual interfaces and panics under
# VirtualBox's NEM fallback execution on hosts where Hyper-V owns hardware VT.
# x-ui: alireza0 v1.14.0 (pinned release tarball + SHA256 verify).
#
# Stages:
#   setup    download + verify artifacts, build the VM, provision x-ui, create the
#            test inbound, apply the SSH tunnel fixture, verify the login contract,
#            poweroff and snapshot
#   provision  apply the SSH tunnel fixture (Docs/25 §5.1) to the existing, running
#            or stopped VM: the forward-only user, its key and the recorded host-key
#            pin. Leaves the VM running; changes nothing on the snapshot
#   refresh  replace the clean-test-state snapshot with one that already carries the
#            tunnel fixture: poweroff -> snapshot restore -> start -> fixture ->
#            verify -> poweroff -> take, delete the old one, rename (the VM never
#            has zero snapshots)
#   reset    poweroff -> snapshot restore -> start headless -> poll the login endpoint
#   start|stop|status|clean    usual lifecycle (clean asks for confirmation)
#
# Configuration (environment variables, all optional):
#   XUI_VM_NAME / XUI_VM_IP         VM name / host-only address
#   XUI_PANEL_PORT                  panel HTTP port          (default 2087)
#   XUI_PANEL_USER / XUI_PANEL_PASSWORD   panel credentials  (default admin / xui.test.123456)
#   XUI_CONSOLE_PASSWORD            cloud-init console password for the 'debian' user,
#                                   for human testing via the VirtualBox console
#                                   (SSH itself is key-only) — default console.test.123456
#   XUI_TUNNEL_USER                 user of the SSH tunnel fixture (default xui-panel):
#                                   key-only, forward-only to the panel's loopback port
#   XUI_TUNNEL_KEY_NAME             file name of that user's key under the key directory
#                                   (default xui-panel-tunnel-key)
#   XUI_INBOUND_PORT / XUI_INBOUND_PATH   test inbound        (default 10000 / /testws)
#   DEBIAN_IMAGE_URL / DEBIAN_IMAGE_SHA512  pinned Debian image (defaults below)
#   DEBIAN_IMAGE_PATH               local qcow2 copy instead of downloading (skips verify)
#   XUI_TARBALL_URL / XUI_TARBALL_SHA256    pinned x-ui release (defaults below)
#   XUI_TARBALL_PATH                local tarball copy instead of downloading
#   VBOXMANAGE                      VBoxManage path (default: VBoxManage)
#   VM_CPUS / VM_MEMORY_MB          VM sizing (default 1 / 1024; the disk keeps
#                                   the image's native 3GB — resizing it breaks
#                                   the boot on some VirtualBox hosts)
#
# Notes:
#   - Downloads honor https_proxy / http_proxy; the local-file *_PATH overrides exist
#     for restricted networks.
#   - The host-only network 192.168.56.1/24 (vboxnet0 or equivalent) is created in
#     preflight when missing — the same network as the Mikrotik test VMs.
#   - The seed ISO embeds the generated SSH public key and the console password; the
#     private key lives outside the repo (XDG data dir) and is reused across runs.
#   - The tunnel fixture keeps its key and the recorded host-key pin next to that key,
#     in the key directory; both are produced by `provision` (or `setup`) and read by
#     the tunnel tests (Backend/Tests/appsettings.json, TestVmOptions).
#   - The snapshot storage of the VM lives in the cache directory: clearing the cache
#     destroys the VM together with its snapshots.
#   - Windows hosts (running from WSL against the Windows VirtualBox): keep
#     VBOXMANAGE=VBoxManage.exe (auto-selected via WSL interop) and put the cache on
#     a Windows-visible path (e.g. XDG_CACHE_HOME=/mnt/d/.cache). Path arguments are
#     translated with wslpath automatically; SSH/curl to the VM go over the host-only
#     network, which WSL reaches through the Windows host.
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd -- "$SCRIPT_DIR/.." && pwd)
LOG_DIR="$SCRIPT_DIR/logs"
CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/photon-bypass/xui-test-vm"
# SSH keys live outside the cache on purpose: on Windows hosts the cache sits on a
# drvfs mount (/mnt/...) where every file is 0777 and ssh refuses the private key
KEY_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/photon-bypass/xui-test-vm"

# --- defaults ---------------------------------------------------------------------

XUI_VM_NAME=${XUI_VM_NAME:-Xui-Test}
XUI_VM_IP=${XUI_VM_IP:-192.168.56.13}
XUI_PANEL_PORT=${XUI_PANEL_PORT:-2087}
XUI_PANEL_USER=${XUI_PANEL_USER:-admin}
XUI_PANEL_PASSWORD=${XUI_PANEL_PASSWORD:-xui.test.123456}
XUI_CONSOLE_PASSWORD=${XUI_CONSOLE_PASSWORD:-console.test.123456}
XUI_INBOUND_PORT=${XUI_INBOUND_PORT:-10000}
XUI_INBOUND_PATH=${XUI_INBOUND_PATH:-/testws}
XUI_VERSION=${XUI_VERSION:-v1.14.0}
XUI_TARBALL_URL=${XUI_TARBALL_URL:-"https://github.com/alireza0/x-ui/releases/download/$XUI_VERSION/x-ui-linux-amd64.tar.gz"}
XUI_TARBALL_SHA256=${XUI_TARBALL_SHA256:-fe733b30c4d6dec538406b8f2e3e2d4eda506684b93d89865ceef7b10acb2dd6}
XUI_TARBALL_PATH=${XUI_TARBALL_PATH:-}
DEBIAN_SNAPSHOT=${DEBIAN_SNAPSHOT:-20261117-2665}
DEBIAN_IMAGE_URL=${DEBIAN_IMAGE_URL:-"https://cloud.debian.org/images/cloud/bookworm/$DEBIAN_SNAPSHOT/debian-12-generic-amd64-$DEBIAN_SNAPSHOT.qcow2"}
DEBIAN_IMAGE_SHA512=${DEBIAN_IMAGE_SHA512:-d597a8b422a0fa7f7cf1a0477d23be7f7514f32e57ccb8d0c69156e1be4a7e1712fb64aee6298520d70aa573b801d0cfe3392ab5de39829728760c0cf99ef4ec}
DEBIAN_IMAGE_PATH=${DEBIAN_IMAGE_PATH:-}
HOSTONLY_SUBNET_IP=${HOSTONLY_SUBNET_IP:-192.168.56.1}
HOSTONLY_NETMASK=${HOSTONLY_NETMASK:-255.255.255.0}
VBOXMANAGE=${VBOXMANAGE:-VBoxManage}
# default 1 vCPU: the Debian guest panics in the initramfs init with more than
# one vCPU under VirtualBox's NEM execution (hosts where Hyper-V owns hardware
# VT, e.g. WSL2); the test panel does not need more
VM_CPUS=${VM_CPUS:-1}
VM_MEMORY_MB=${VM_MEMORY_MB:-1024}
SNAPSHOT=${SNAPSHOT:-clean-test-state}
# working name of the fixture snapshot a refresh takes before it replaces SNAPSHOT
FIXTURE_SNAPSHOT=${FIXTURE_SNAPSHOT:-tunnel-fixture-state}
SSH_USER=${SSH_USER:-debian}
SSH_TIMEOUT_SECONDS=${SSH_TIMEOUT_SECONDS:-600}
LOGIN_TIMEOUT_SECONDS=${LOGIN_TIMEOUT_SECONDS:-120}
XUI_TUNNEL_USER=${XUI_TUNNEL_USER:-xui-panel}
XUI_TUNNEL_KEY_NAME=${XUI_TUNNEL_KEY_NAME:-xui-panel-tunnel-key}
XUI_TUNNEL_KEY_COMMENT=${XUI_TUNNEL_KEY_COMMENT:-photon-test}

STAGE=""
ASSUME_YES=false
FAILED=false
LOG_FILE=""

# --- Windows-host (WSL) support -----------------------------------------------------

# When running from WSL against the Windows VirtualBox, VBoxManage is the Windows
# executable and file arguments must be Windows paths. Auto-detected, or forced
# with VBOX_ON_WINDOWS=1/0. See the Windows-hosts note in the header.
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
    if ! "$VBOXMANAGE" "$@" 2>&1 | tee -a "$LOG_FILE"; then
        die "VBoxManage failed: $*"
    fi
}

vbox_quiet() {
    # tr -d '\r': VBoxManage.exe emits CRLF line endings, which would corrupt
    # awk/sed parsing of its output on Windows hosts
    "$VBOXMANAGE" "$@" 2>>"$LOG_FILE" | tr -d '\r'
}

iso_tool() { # prints the mkisofs-compatible invocation prefix
    if command -v genisoimage >/dev/null 2>&1; then
        printf 'genisoimage'
    elif command -v mkisofs >/dev/null 2>&1; then
        printf 'mkisofs'
    elif command -v xorriso >/dev/null 2>&1; then
        printf 'xorriso -as mkisofs'
    else
        die "none of genisoimage / mkisofs / xorriso found (Debian/Ubuntu: apt-get install genisoimage)"
    fi
}

require_runtime_tools() { # every stage that talks to the VM
    command -v "$VBOXMANAGE" >/dev/null 2>&1 || die "VBoxManage not found (env VBOXMANAGE can point at it)"
    command -v jq >/dev/null 2>&1 || die "jq not found (Debian/Ubuntu: apt-get install jq)"
    command -v curl >/dev/null 2>&1 || die "curl not found"
    command -v ssh >/dev/null 2>&1 || die "ssh not found"
    command -v scp >/dev/null 2>&1 || die "scp not found"
    command -v ssh-keygen >/dev/null 2>&1 || die "ssh-keygen not found"
}

require_tools() { # setup: the runtime tools plus the image build chain
    require_runtime_tools
    command -v qemu-img >/dev/null 2>&1 || die "qemu-img not found (Debian/Ubuntu: apt-get install qemu-utils)"
    command -v sha512sum >/dev/null 2>&1 || die "sha512sum not found"
    command -v sha256sum >/dev/null 2>&1 || die "sha256sum not found"
    iso_tool >/dev/null
}

# --- VirtualBox helpers -----------------------------------------------------------

vm_exists() {
    vbox_quiet list vms | awk -F'"' -v name="$1" '$2 == name { found = 1 } END { exit !found }'
}

vm_running() {
    vbox_quiet list runningvms | awk -F'"' -v name="$1" '$2 == name { found = 1 } END { exit !found }'
}

abort_if_registered() {
    if vm_exists "$XUI_VM_NAME"; then
        die "a VM named '$XUI_VM_NAME' is already registered — refusing to touch it. If it is a leftover: $0 clean"
    fi
}

ensure_hostonly_network() {
    local existing
    # Windows host-only names contain spaces ("VirtualBox Host-Only Ethernet
    # Adapter"), so capture the whole value, not just the second field
    existing=$(vbox_quiet list hostonlyifs | awk -v ip="$HOSTONLY_SUBNET_IP" '
        /^Name:/      { name = $0; sub(/^Name:[[:space:]]*/, "", name) }
        /^IPAddress:/ { if ($2 == ip) print name }')
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

wait_state_off() { # $1=vm, $2=timeout-seconds
    local vm=$1 timeout_s=$2 start
    start=$(date +%s)
    while [ $(( $(date +%s) - start )) -lt "$timeout_s" ]; do
        if ! vm_running "$vm"; then
            return 0
        fi
        sleep 3
    done
    return 1
}

poweroff_vm() {
    if vm_running "$1"; then
        vbox controlvm "$1" poweroff
        sleep 2
    else
        log "vm $1 is not running"
    fi
}

graceful_poweroff() { # $1=vm — a clean guest shutdown before a snapshot; hard poweroff only as the fallback
    vm_ssh 'sudo systemctl poweroff' >/dev/null 2>&1 || true
    if wait_state_off "$1" 90; then
        return
    fi
    log "graceful poweroff of $1 timed out — powering off"
    poweroff_vm "$1"
}

has_snapshot() { # $1=vm, $2=snapshot name
    vbox_quiet snapshot "$1" list --machinereadable \
        | grep -Eq "^SnapshotName(-[0-9]+)?=\"$2\"\$"
}

free_local_port() { # prints a loopback TCP port nothing listens on (for the tunnel check of the script)
    local port
    for port in $(seq 28870 28899); do
        if ! timeout 1 bash -c "</dev/tcp/127.0.0.1/$port" 2>/dev/null; then
            printf '%s\n' "$port"
            return 0
        fi
    done
    return 1
}

# --- artifacts --------------------------------------------------------------------

download_file() { # $1=url, $2=destination, $3=reason
    [ -f "$2" ] && return
    note "downloading $3 (honors https_proxy)"
    curl -fsSL --retry 3 --output "$2.part" "$1" || die "download failed: $1"
    mv "$2.part" "$2"
}

debian_qcow2() { # prints the path of the pinned Debian cloud image
    local image_name target
    image_name=$(basename "$DEBIAN_IMAGE_URL")
    [ -n "$image_name" ] || die "DEBIAN_IMAGE_URL has no file name"
    target="$CACHE_DIR/$image_name"

    if [ -n "$DEBIAN_IMAGE_PATH" ]; then
        [ -f "$DEBIAN_IMAGE_PATH" ] || die "DEBIAN_IMAGE_PATH not found: $DEBIAN_IMAGE_PATH"
        printf '%s\n' "$DEBIAN_IMAGE_PATH"
        return
    fi

    download_file "$DEBIAN_IMAGE_URL" "$target" "Debian $DEBIAN_SNAPSHOT genericcloud image"
    printf '%s  %s\n' "$DEBIAN_IMAGE_SHA512" "$target" | sha512sum -c - >/dev/null \
        || die "Debian image checksum mismatch — update DEBIAN_SNAPSHOT/DEBIAN_IMAGE_SHA512 or use DEBIAN_IMAGE_PATH"
    printf '%s\n' "$target"
}

xui_tarball() { # prints the path of the pinned x-ui release tarball
    local target="$CACHE_DIR/x-ui-linux-amd64-$XUI_VERSION.tar.gz"

    if [ -n "$XUI_TARBALL_PATH" ]; then
        [ -f "$XUI_TARBALL_PATH" ] || die "XUI_TARBALL_PATH not found: $XUI_TARBALL_PATH"
        printf '%s\n' "$XUI_TARBALL_PATH"
        return
    fi

    download_file "$XUI_TARBALL_URL" "$target" "x-ui $XUI_VERSION release tarball"
    printf '%s  %s\n' "$XUI_TARBALL_SHA256" "$target" | sha256sum -c - >/dev/null \
        || die "x-ui tarball checksum mismatch — update XUI_TARBALL_SHA256 or use XUI_TARBALL_PATH"
    printf '%s\n' "$target"
}

ensure_ssh_key() { # $1=key file name under KEY_DIR, $2=comment for a new key -> prints the key path
    local key="$KEY_DIR/$1"
    local options=(-q -t ed25519 -N '' -f "$key")
    mkdir -p "$KEY_DIR"
    if [ ! -f "$key" ]; then
        note "generating SSH keypair '$1' (kept in $KEY_DIR)"
        [ $# -lt 2 ] || options+=(-C "$2")
        ssh-keygen "${options[@]}"
    fi
    printf '%s\n' "$key"
}

provision_key() { # the key of SSH_USER: full sudo on the VM
    ensure_ssh_key xui-test-vm-key
}

tunnel_key() { # the key of the tunnel fixture user (Docs/25 §5.1)
    ensure_ssh_key "$XUI_TUNNEL_KEY_NAME" "$XUI_TUNNEL_KEY_COMMENT"
}

tunnel_pin_file() { # where the host-key pin of the VM is recorded
    printf '%s\n' "$KEY_DIR/$XUI_VM_NAME.hostkey"
}

fixture_file() { # $1=file under KEY_DIR -> its path relative to the key root, the form TestVmOptions resolves
    realpath --relative-to="$(dirname "$KEY_DIR")" "$1"
}

# --- seed ISO ---------------------------------------------------------------------

mac_with_colons() { # VBoxManage prints MACs without separators; netplan requires XX:XX:...
    printf '%s' "$1" | sed -E 's/(..)/\1:/g; s/:$//' | tr 'A-F' 'a-f'
}

build_seed_iso() { # $1=vm, $2=ssh-key-path, $3=mac-nat, $4=mac-hostonly -> prints iso path
    local seed_dir="$CACHE_DIR/$1-seed"
    local iso="$CACHE_DIR/$1-seed.iso"

    rm -rf "$seed_dir"
    mkdir -p "$seed_dir"

    cat >"$seed_dir/meta-data" <<EOF
instance-id: $1-$(date +%Y%m%d%H%M%S)
local-hostname: $1
EOF

    cat >"$seed_dir/user-data" <<EOF
#cloud-config
hostname: $1
ssh_pwauth: false
users:
  - name: $SSH_USER
    plain_text_passwd: "$XUI_CONSOLE_PASSWORD"
    lock_passwd: false
    shell: /bin/bash
    sudo: ALL=(ALL) NOPASSWD:ALL
    ssh_authorized_keys:
      - $(cat "$2.pub")
chpasswd:
  expire: false
EOF
    chmod 600 "$seed_dir/user-data"

    # No set-name here and colon-separated MACs: netplan (the renderer on Debian)
    # rejects dash-less MACs ("Invalid MAC address ... must be XX:XX:..."), which
    # fails 'netplan generate' and leaves the VM without any network at all.
    cat >"$seed_dir/network-config" <<EOF
version: 2
ethernets:
  nic-nat:
    match:
      macaddress: "$(mac_with_colons "$3")"
    dhcp4: true
  nic-hostonly:
    match:
      macaddress: "$(mac_with_colons "$4")"
    dhcp4: false
    addresses:
      - $XUI_VM_IP/24
EOF

    note "building NoCloud seed ISO (label: cidata)"
    # shellcheck disable=SC2046
    $(iso_tool) -output "$iso" -volid cidata -joliet -rock "$seed_dir" >>"$LOG_FILE" 2>&1 \
        || die "seed ISO build failed"

    printf '%s\n' "$iso"
}

# --- VM access --------------------------------------------------------------------

wait_inbound_port() { # xray must actually run the test inbound, not just boot the panel
    local start
    start=$(date +%s)
    log "waiting for the test inbound port $XUI_INBOUND_PORT (xray startup, timeout: ${LOGIN_TIMEOUT_SECONDS}s)"
    while [ $(( $(date +%s) - start )) -lt "$LOGIN_TIMEOUT_SECONDS" ]; do
        if timeout 3 bash -c "</dev/tcp/$XUI_VM_IP/$XUI_INBOUND_PORT" 2>/dev/null; then
            return 0
        fi
        sleep 3
    done
    return 1
}

ssh_with_key() { # $1=key, $2=user, rest = remote command; BatchMode: a rejected key must fail, never prompt
    local key=$1 user=$2
    shift 2
    ssh -i "$key" -o IdentitiesOnly=yes -o BatchMode=yes \
        -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \
        -o ConnectTimeout=10 "$user@$XUI_VM_IP" "$@"
}

vm_ssh() { # rest = remote command, as the provisioning user
    ssh_with_key "$KEY_DIR/xui-test-vm-key" "$SSH_USER" "$@"
}

wait_ssh() {
    local start
    start=$(date +%s)
    log "waiting for SSH on $SSH_USER@$XUI_VM_IP (timeout: ${SSH_TIMEOUT_SECONDS}s)"
    while [ $(( $(date +%s) - start )) -lt "$SSH_TIMEOUT_SECONDS" ]; do
        if vm_ssh 'echo ok' >/dev/null 2>&1; then
            log "SSH is up"
            return 0
        fi
        sleep 5
    done
    return 1
}

# --- panel API --------------------------------------------------------------------

panel_login() { # prints the login response body; cookie jar at $CACHE_DIR/panel.cookies
    # --noproxy: the panel is link-local — with a corporate proxy in http_proxy
    # the request would be intercepted and the login contract never seen
    curl -s --max-time 10 --noproxy '*' -c "$CACHE_DIR/panel.cookies" \
        -H 'Content-Type: application/json' \
        -d "{\"username\":\"$XUI_PANEL_USER\",\"password\":\"$XUI_PANEL_PASSWORD\"}" \
        "http://$XUI_VM_IP:$XUI_PANEL_PORT/login"
}

wait_panel_login() {
    local start
    start=$(date +%s)
    log "waiting for the panel login endpoint (timeout: ${LOGIN_TIMEOUT_SECONDS}s)"
    while [ $(( $(date +%s) - start )) -lt "$LOGIN_TIMEOUT_SECONDS" ]; do
        local body
        body=$(panel_login || true)
        if [ "$(printf '%s' "$body" | jq -r '.success // empty' 2>/dev/null || true)" = "true" ] \
            && [ -s "$CACHE_DIR/panel.cookies" ]; then
            log "panel login endpoint is ready"
            return 0
        fi
        sleep 5
    done
    return 1
}

panel_post() { # $1=path, $2=json-body
    curl -s --max-time 15 --noproxy '*' -b "$CACHE_DIR/panel.cookies" \
        -H 'Content-Type: application/json' -H 'X-Requested-With: XMLHttpRequest' \
        -d "$2" "http://$XUI_VM_IP:$XUI_PANEL_PORT/$1"
}

create_test_inbound() { # prints the created inbound id
    # settings / streamSettings / sniffing are JSON *strings* inside the add
    # payload, not nested objects — the panel rejects an inline object with
    # "json: cannot unmarshal object into Go struct field Inbound.sniffing of
    # type string" (verified live, alireza0 v1.14.0; same rule as
    # XuiInboundModel.StreamSettings in this repo)
    local settings stream sniffing payload response
    settings=$(jq -nc '{clients: [], decryption: "none"}')
    stream=$(jq -nc --arg path "$XUI_INBOUND_PATH" \
        '{network: "ws", security: "none", wsSettings: {path: $path}}')
    sniffing=$(jq -nc '{enabled: true, destOverride: ["http", "tls", "quic"]}')
    payload=$(jq -nc \
        --argjson port "$XUI_INBOUND_PORT" \
        --arg settings "$settings" \
        --arg stream "$stream" \
        --arg sniffing "$sniffing" \
        '{up: 0, down: 0, total: 0, remark: "test-inbound", enable: true, expiryTime: 0,
          listen: "0.0.0.0", port: $port, protocol: "vless",
          settings: $settings, streamSettings: $stream, sniffing: $sniffing}')

    note "creating the test inbound (VLESS+ws, port $XUI_INBOUND_PORT, path $XUI_INBOUND_PATH)"
    response=$(panel_post 'xui/API/inbounds/add' "$payload")
    [ "$(printf '%s' "$response" | jq -r '.success // empty' 2>/dev/null || true)" = "true" ] \
        || die "inbound add failed: $(printf '%s' "$response" | jq -c '{success, msg}' 2>/dev/null || printf 'unparseable response')"

    local list inbound_id
    list=$(curl -s --max-time 15 --noproxy '*' -b "$CACHE_DIR/panel.cookies" \
        -H 'X-Requested-With: XMLHttpRequest' \
        "http://$XUI_VM_IP:$XUI_PANEL_PORT/xui/API/inbounds/")
    inbound_id=$(printf '%s' "$list" | jq -r --argjson port "$XUI_INBOUND_PORT" \
        '.obj[] | select(.port == $port) | .id' 2>/dev/null | head -n 1)
    [ -n "$inbound_id" ] || die "could not read back the created inbound id (port $XUI_INBOUND_PORT)"
    note "inbound created: id=$inbound_id"
    printf '%s\n' "$inbound_id"
}

configure_freedom_allow() { # $1 = host-only subnet cidr (e.g. 192.168.56.0/24)
    # xray >= 25.x ships a default safety policy on the freedom outbound: proxy
    # inbound traffic to private/reserved targets is blackholed
    # ("proxy/freedom: blocked target ... blackholing"). The data-plane
    # integration tests (Backend/Tests/Facts/Xui, Docs/23 §4.4) tunnel to the
    # panel/sshd on this same VM over the host-only network, so the direct
    # outbound needs an explicit finalRules allow for that subnet + loopback
    # (verified live on x-ui v1.14.0 / xray 26.10.10, 2026-11-21).
    local rules
    rules=$(printf '[{"action":"allow","ip":["%s","127.0.0.0/8"]}]' "$1")

    note "allowing $1 + loopback on the direct freedom outbound (finalRules)"
    # the SQL travels over stdin: its double quotes would otherwise terminate the
    # remote shell quoting, and json() matters — without it sqlite stores the
    # array as a JSON *string* and xray refuses to start ("cannot unmarshal
    # string into Go struct field FreedomConfig.finalRules")
    printf "UPDATE outbounds SET settings = json_set(settings, '\$.finalRules', json('%s')) WHERE tag = 'direct';\n" "$rules" \
        | vm_ssh sudo sqlite3 /etc/x-ui/x-ui.db \
        || die "could not patch the direct outbound finalRules"
    vm_ssh sudo systemctl restart x-ui

    wait_panel_login || die "the panel did not come back after the freedom-allow patch"
    wait_inbound_port || die "the test inbound (port $XUI_INBOUND_PORT) is not reachable — xray may have failed to start; check 'journalctl -u x-ui' on the VM"
    log "inbound port $XUI_INBOUND_PORT is answering (xray up)"
}

# --- provisioning -----------------------------------------------------------------

write_provision_script() { # $1=tarball-file-name -> prints script path
    local script="$CACHE_DIR/provision.sh"

    cat >"$script" <<'EOF'
#!/usr/bin/env bash
# Runs inside the VM (root) — values are injected below by the host script.
set -euo pipefail

XUI_PANEL_PORT=__XUI_PANEL_PORT__
XUI_PANEL_USER=__XUI_PANEL_USER__
XUI_PANEL_PASSWORD=__XUI_PANEL_PASSWORD__
TARBALL=__TARBALL__

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq --no-install-recommends sqlite3 ca-certificates >/dev/null

# extract the release tarball: it contains an x-ui/ directory with the binary,
# the default database and the bundled xray core
rm -rf /usr/local/x-ui /tmp/xui-extract
mkdir -p /usr/local/x-ui /tmp/xui-extract
tar -xzf "/tmp/$TARBALL" -C /tmp/xui-extract
if [ -d /tmp/xui-extract/x-ui ]; then
    cp -a /tmp/xui-extract/x-ui/. /usr/local/x-ui/
else
    cp -a /tmp/xui-extract/. /usr/local/x-ui/
fi
rm -rf /tmp/xui-extract
chmod +x /usr/local/x-ui/x-ui
mkdir -p /etc/x-ui
[ -f /etc/x-ui/x-ui.db ] || cp /usr/local/x-ui/x-ui.db /etc/x-ui/x-ui.db 2>/dev/null || true

cat >/etc/systemd/system/x-ui.service <<'UNIT'
[Unit]
Description=x-ui Service (local test panel)
After=network.target nss-lookup.target

[Service]
Type=simple
WorkingDirectory=/usr/local/x-ui
ExecStart=/usr/local/x-ui/x-ui
Restart=on-failure
RestartSec=5s

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable x-ui >/dev/null

# Configure the panel non-interactively via the bundled CLI; the exact flags are
# probed at runtime (-flag and --flag spellings both exist across versions).
configured=no
for style in "-username __XUI_PANEL_USER__ -password __XUI_PANEL_PASSWORD__ -port __XUI_PANEL_PORT__" \
             "--username __XUI_PANEL_USER__ --password __XUI_PANEL_PASSWORD__ --port __XUI_PANEL_PORT__"; do
    if /usr/local/x-ui/x-ui setting $style >/dev/null 2>&1; then
        configured=yes
        break
    fi
done

if [ "$configured" != yes ]; then
    # Fallback: edit the sqlite database directly (alireza0 layout).
    DB=/etc/x-ui/x-ui.db
    sqlite3 "$DB" "UPDATE settings SET value='__XUI_PANEL_PORT__' WHERE key='webPort';"
    if sqlite3 "$DB" ".tables" | grep -qw users; then
        sqlite3 "$DB" "UPDATE users SET username='__XUI_PANEL_USER__', password='__XUI_PANEL_PASSWORD__';" 2>/dev/null || true
    fi
    for key in webUsername webPassword; do
        value='__XUI_PANEL_USER__'
        [ "$key" = webPassword ] && value='__XUI_PANEL_PASSWORD__'
        sqlite3 "$DB" "UPDATE settings SET value='$value' WHERE key='$key';" 2>/dev/null || true
    done
fi

systemctl restart x-ui
sleep 5
systemctl is-active x-ui
EOF

    local content tarball_name
    tarball_name=$(basename "$1")
    content=$(cat "$script")
    content=${content//__XUI_PANEL_PORT__/$XUI_PANEL_PORT}
    content=${content//__XUI_PANEL_USER__/$XUI_PANEL_USER}
    content=${content//__XUI_PANEL_PASSWORD__/$XUI_PANEL_PASSWORD}
    content=${content//__TARBALL__/$tarball_name}
    printf '%s' "$content" >"$script"
    chmod 600 "$script"

    printf '%s\n' "$script"
}

# --- SSH tunnel fixture (Docs/25 §5.1) ----------------------------------------------
#
# The panel reaches this VM's x-ui API through an SSH loopback port-forward: one
# forward-only user, authenticated with its own key, whose authorized_keys line
# permits exactly one target — the panel on the VM's own loopback. The key and the
# recorded host-key pin stay outside the repo; the tunnel tests read them through
# Backend/Tests/appsettings.json (TestVmOptions).

apply_tunnel_fixture() { # the user and its forward-only line; safe to repeat
    local key type body comment
    key=$(tunnel_key)
    read -r type body comment <"$key.pub"
    note "applying the tunnel fixture: user '$XUI_TUNNEL_USER', forward to 127.0.0.1:$XUI_PANEL_PORT"
    # the script travels over stdin and the public key as plain words: neither would
    # survive the remote-shell quoting of a one-line command
    vm_ssh sudo bash -s -- "$XUI_TUNNEL_USER" "$XUI_PANEL_PORT" "$type" "$body" "$comment" <<'REMOTE' >>"$LOG_FILE" 2>&1 \
        || die "the tunnel fixture could not be applied — see $LOG_FILE"
set -euo pipefail
user=$1 panel_port=$2 key_type=$3 key_body=$4 key_comment=$5

if id -u "$user" >/dev/null 2>&1; then
    echo "user $user exists"
else
    useradd --create-home --shell /bin/bash "$user"
    echo "user $user created"
fi

home=$(getent passwd "$user" | cut -d: -f6)
chmod 755 "$home"
install -d -m 700 -o "$user" -g "$user" "$home/.ssh"
auth="$home/.ssh/authorized_keys"
touch "$auth"
chown "$user:$user" "$auth"
chmod 600 "$auth"

if grep -qF -- "$key_body" "$auth"; then
    echo "authorized_keys already carries the key"
else
    # a file without a trailing newline would glue the new line onto the comment of the previous key (Docs/25 appendix B)
    if [ -s "$auth" ] && [ -n "$(tail -c1 "$auth")" ]; then echo >>"$auth"; fi
    printf '%s\n' "restrict,port-forwarding,permitopen=\"127.0.0.1:$panel_port\",command=\"/bin/false\" $key_type $key_body $key_comment" >>"$auth"
    echo "authorized_keys line added"
fi

ls -ld "$home" "$home/.ssh" "$auth"
REMOTE
}

write_host_key_pin() { # records the fingerprint of the key the VM's sshd presents: what the panel pins (Docs/25 D4)
    local key pin pin_file
    key=$(provision_key)
    pin_file=$(tunnel_pin_file)

    # one throw-away connection: `ssh -v` prints the presented key before any
    # authentication (its debug lines end in CRLF)
    pin=$({ ssh -i "$key" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=no \
        -o UserKnownHostsFile=/dev/null -o ConnectTimeout=10 -v "$SSH_USER@$XUI_VM_IP" true 2>&1 >/dev/null || true; } \
        | tr -d '\r' | sed -n 's|^debug1: Server host key: [^ ]* \(SHA256:[A-Za-z0-9+/]\{43\}\)$|\1|p')
    [[ "$pin" =~ ^SHA256:[A-Za-z0-9+/]{43}$ ]] \
        || die "could not read the host-key fingerprint of $XUI_VM_IP — is SSH up on the VM?"
    printf '%s\n' "$pin" >"$pin_file"
    log "host-key pin of $XUI_VM_NAME: $pin ($(fixture_file "$pin_file"))"
}

verify_tunnel_fixture() { # the two contract points of the restricted key (Docs/25 §5.1)
    local key status out port forward_pid body
    key=$(tunnel_key)

    out=$(ssh_with_key "$key" "$XUI_TUNNEL_USER" id 2>/dev/null) && status=0 || status=$?
    [ "$status" -ne 0 ] && [ -z "$out" ] \
        || die "exec with the tunnel key must end without output and a non-zero status (got status $status, output '$out')"

    port=$(free_local_port) || die "no free loopback port for the forward check"
    ssh -i "$key" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=no \
        -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ExitOnForwardFailure=yes \
        -o ServerAliveInterval=5 -N -L "127.0.0.1:$port:127.0.0.1:$XUI_PANEL_PORT" \
        "$XUI_TUNNEL_USER@$XUI_VM_IP" &
    forward_pid=$!
    body=$(curl -s --max-time 10 --retry 10 --retry-connrefused --retry-delay 1 --retry-max-time 30 --noproxy '*' \
        -H 'Content-Type: application/json' \
        -d "{\"username\":\"$XUI_PANEL_USER\",\"password\":\"$XUI_PANEL_PASSWORD\"}" "http://127.0.0.1:$port/login" || true)
    kill "$forward_pid" 2>/dev/null || true
    wait "$forward_pid" 2>/dev/null || true
    [ "$(printf '%s' "$body" | jq -r '.success // empty' 2>/dev/null || true)" = "true" ] \
        || die "the panel login did not answer through the tunnel (got: '${body:-nothing}')"
    log "tunnel fixture verified on $XUI_VM_NAME: exec is refused, the panel login answers through the forward"
}

verify_inbound_empty() { # the state the client tests start from: the test inbound exists and carries no client
    local count
    panel_login >/dev/null
    count=$(curl -s --max-time 15 --noproxy '*' -b "$CACHE_DIR/panel.cookies" -H 'X-Requested-With: XMLHttpRequest' \
        "http://$XUI_VM_IP:$XUI_PANEL_PORT/xui/API/inbounds/" \
        | jq -r --argjson port "$XUI_INBOUND_PORT" \
            '[.obj[] | select(.port == $port) | .settings | fromjson | .clients | length] | join(",")' 2>/dev/null || true)
    [ "$count" = "0" ] \
        || die "the test inbound (port $XUI_INBOUND_PORT) must exist without clients — got: '${count:-nothing}'"
}

print_tunnel_fixture() { # what setup and provision leave behind for the tests
    cat <<EOF

  tunnel fixture (Docs/25 §5.1):
    user:        $XUI_TUNNEL_USER@$XUI_VM_IP — key-only, forward-only to 127.0.0.1:$XUI_PANEL_PORT
    key:         $(tunnel_key)
    host key:    $(cat "$(tunnel_pin_file)")  ($(tunnel_pin_file))

TestVmOptions fields of the "$XUI_VM_NAME" entry (key root: $(dirname "$KEY_DIR")):

  "TunnelUser": "$XUI_TUNNEL_USER", "TunnelKeyFile": "$(fixture_file "$(tunnel_key)")", "HostKeyFile": "$(fixture_file "$(tunnel_pin_file)")",
  "AdminUser": "$SSH_USER", "AdminKeyFile": "$(fixture_file "$(provision_key)")"
EOF
}

apply_tunnel_fixture_chain() { # the three fixture functions in their order, shared by setup, provision and refresh
    apply_tunnel_fixture
    write_host_key_pin
    verify_tunnel_fixture
}

# --- stages -----------------------------------------------------------------------

run_stage_setup() {
    local hostonly_if seed_iso ssh_key tarball disk mac_nat mac_hostonly

    require_tools
    abort_if_registered
    hostonly_if=$(ensure_hostonly_network)
    ssh_key=$(provision_key)
    tarball=$(xui_tarball)
    disk="$CACHE_DIR/$XUI_VM_NAME-disk.vdi"

    log "converting the Debian image to VDI (native 3GB size)"
    rm -f "$disk"
    # direct conversion, no resize: a resized disk (qemu or VBoxManage) makes the
    # guest panic in the initramfs init under some VirtualBox hosts (NEM). The
    # image's native 3GB leaves ~1.3GB free — plenty for the test panel.
    qemu-img convert -f qcow2 -O vdi "$(debian_qcow2)" "$disk"

    log "creating VM $XUI_VM_NAME ($XUI_VM_IP)"
    vbox createvm --name "$XUI_VM_NAME" --ostype Debian_64 --register
    # --paravirt-provider minimal: the Debian cloud kernel latches onto the KVM
    # paravirtual interfaces VBox presents (kvm-clock/async-pf) and panics under
    # VBox's NEM fallback execution (hosts where Hyper-V owns hardware VT, e.g.
    # WSL2). Minimal keeps the guest on native timers.
    vbox modifyvm "$XUI_VM_NAME" --cpus "$VM_CPUS" --memory "$VM_MEMORY_MB" --vram 16 --ioapic on \
        --paravirt-provider minimal \
        --nic1 nat --nictype1 virtio \
        --nic2 hostonly --nictype2 virtio --hostonlyadapter2 "$hostonly_if"
    # Debian cloud images are built for virtio: their initramfs carries the
    # virtio drivers but not AHCI, and the PIIX IDE disk path panics this kernel
    # under VirtualBox without hardware VT (NEM). The root disk goes on a
    # virtio-scsi controller; the NoCloud seed ISO stays on IDE, which only
    # needs the CD drivers.
    vbox storagectl "$XUI_VM_NAME" --name VIO --add virtio-scsi --bootable on
    vbox storagectl "$XUI_VM_NAME" --name IDE --add ide
    vbox storageattach "$XUI_VM_NAME" --storagectl VIO --port 0 --device 0 --type hdd --medium "$(wp "$disk")"

    mac_nat=$(vbox_quiet showvminfo "$XUI_VM_NAME" --machinereadable | awk -F'"' '/^macaddress1=/{print $2}')
    mac_hostonly=$(vbox_quiet showvminfo "$XUI_VM_NAME" --machinereadable | awk -F'"' '/^macaddress2=/{print $2}')
    [ -n "$mac_nat" ] && [ -n "$mac_hostonly" ] || die "could not read the NIC MAC addresses"

    seed_iso=$(build_seed_iso "$XUI_VM_NAME" "$ssh_key" "$mac_nat" "$mac_hostonly")
    vbox storageattach "$XUI_VM_NAME" --storagectl IDE --port 1 --device 0 --type dvddrive --medium "$(wp "$seed_iso")"

    log "starting $XUI_VM_NAME headless (first boot runs cloud-init)"
    vbox startvm "$XUI_VM_NAME" --type headless

    wait_ssh || die "SSH did not come up on $XUI_VM_IP — check 'VBoxManage showvminfo $XUI_VM_NAME --log vga' or boot with --type gui"

    log "provisioning x-ui $XUI_VERSION inside the VM"
    scp -q -i "$ssh_key" -o IdentitiesOnly=yes \
        -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \
        "$tarball" "$SSH_USER@$XUI_VM_IP:/tmp/"
    local provision
    provision=$(write_provision_script "$tarball")
    scp -q -i "$ssh_key" -o IdentitiesOnly=yes \
        -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \
        "$provision" "$SSH_USER@$XUI_VM_IP:/tmp/provision.sh"
    vm_ssh 'sudo bash /tmp/provision.sh' >>"$LOG_FILE" 2>&1 \
        || die "provisioning failed — see $LOG_FILE"

    wait_panel_login || die "the panel login endpoint did not become ready (expected success:true + session cookie — the XuiApiService contract)"

    local inbound_id
    inbound_id=$(create_test_inbound)

    configure_freedom_allow "${HOSTONLY_SUBNET_IP%.*}.0/24"

    apply_tunnel_fixture_chain

    log "powering off for the snapshot"
    graceful_poweroff "$XUI_VM_NAME"
    vbox storageattach "$XUI_VM_NAME" --storagectl IDE --port 1 --device 0 --type dvddrive --medium emptydrive
    vbox snapshot "$XUI_VM_NAME" take "$SNAPSHOT"
    log "snapshot '$SNAPSHOT' taken"

    cat <<EOF

OK — x-ui test VM ready.

  panel:      http://$XUI_VM_IP:$XUI_PANEL_PORT  ($XUI_PANEL_USER / $XUI_PANEL_PASSWORD)
  inbound:    id=$inbound_id  VLESS+ws  port $XUI_INBOUND_PORT  path $XUI_INBOUND_PATH
  ssh:        $SSH_USER@$XUI_VM_IP with key $ssh_key
  console:    VirtualBox console, $SSH_USER / XUI_CONSOLE_PASSWORD (env at setup time)

TestVmOptions entry for Backend/Tests/appsettings.json (plus the tunnel fields below):

  "$XUI_VM_NAME": { "Ip": "$XUI_VM_IP", "Port": $XUI_PANEL_PORT, "Username": "$XUI_PANEL_USER", "Password": "$XUI_PANEL_PASSWORD", "InboundId": $inbound_id,
                    ...tunnel fields... }
EOF
    print_tunnel_fixture

    cat <<EOF

Reset semantics: '$SNAPSHOT' — restore per test run (the XuiPanelInitializer does this automatically).
EOF
}

run_stage_provision() {
    require_runtime_tools
    vm_exists "$XUI_VM_NAME" || die "VM '$XUI_VM_NAME' is not registered"
    if ! vm_running "$XUI_VM_NAME"; then
        log "starting $XUI_VM_NAME headless"
        vbox startvm "$XUI_VM_NAME" --type headless
    fi
    wait_ssh || die "SSH did not come up on $XUI_VM_IP"
    wait_panel_login || die "the panel login endpoint did not become ready"

    apply_tunnel_fixture_chain

    log "OK — tunnel fixture provisioned on the running $XUI_VM_NAME (the snapshot is unchanged; 'refresh' freezes it)"
    print_tunnel_fixture
}

run_stage_refresh() {
    # Rebuilds '$SNAPSHOT' so that it already carries the tunnel fixture. The
    # fixture is applied to a restored clean state, never to the current (dirty)
    # one: test residue must not reach the snapshot.
    require_runtime_tools
    vm_exists "$XUI_VM_NAME" || die "VM '$XUI_VM_NAME' is not registered"

    # a previous refresh that stopped between the delete and the rename
    if ! has_snapshot "$XUI_VM_NAME" "$SNAPSHOT"; then
        has_snapshot "$XUI_VM_NAME" "$FIXTURE_SNAPSHOT" \
            || die "VM '$XUI_VM_NAME' has neither '$SNAPSHOT' nor '$FIXTURE_SNAPSHOT' — nothing to refresh from"
        log "finishing an interrupted refresh: renaming '$FIXTURE_SNAPSHOT' to '$SNAPSHOT'"
        vbox snapshot "$XUI_VM_NAME" edit "$FIXTURE_SNAPSHOT" --name "$SNAPSHOT"
    fi

    log "refreshing $XUI_VM_NAME: restore '$SNAPSHOT' -> tunnel fixture -> verify -> new '$SNAPSHOT'"
    poweroff_vm "$XUI_VM_NAME"
    vbox snapshot "$XUI_VM_NAME" restore "$SNAPSHOT"
    vbox startvm "$XUI_VM_NAME" --type headless
    wait_ssh || die "SSH did not come up on $XUI_VM_IP after the snapshot restore"
    wait_panel_login || die "the panel login endpoint did not become ready after the snapshot restore"

    apply_tunnel_fixture_chain
    verify_inbound_empty

    log "powering off for the snapshot"
    graceful_poweroff "$XUI_VM_NAME"

    # take -> delete -> rename: the VM never has zero snapshots. Interrupted before
    # the delete: '$SNAPSHOT' is intact and this stage is simply repeated (the stale
    # working snapshot is dropped first); interrupted after it: '$FIXTURE_SNAPSHOT'
    # is the verified fixture state and only the rename is left, done at the next run.
    if has_snapshot "$XUI_VM_NAME" "$FIXTURE_SNAPSHOT"; then
        vbox snapshot "$XUI_VM_NAME" delete "$FIXTURE_SNAPSHOT"
    fi
    vbox snapshot "$XUI_VM_NAME" take "$FIXTURE_SNAPSHOT"
    vbox snapshot "$XUI_VM_NAME" delete "$SNAPSHOT"
    vbox snapshot "$XUI_VM_NAME" edit "$FIXTURE_SNAPSHOT" --name "$SNAPSHOT"

    log "OK — '$SNAPSHOT' of $XUI_VM_NAME now holds the tunnel fixture (VM powered off)"
    print_tunnel_fixture
}

run_stage_reset() {
    require_tools
    vm_exists "$XUI_VM_NAME" || die "VM '$XUI_VM_NAME' is not registered"
    log "resetting $XUI_VM_NAME: poweroff -> snapshot '$SNAPSHOT' -> start headless"
    poweroff_vm "$XUI_VM_NAME"
    vbox snapshot "$XUI_VM_NAME" restore "$SNAPSHOT"
    vbox startvm "$XUI_VM_NAME" --type headless
    wait_panel_login || die "the panel login endpoint did not become ready after the snapshot restore"
}

run_stage_start() {
    vm_exists "$XUI_VM_NAME" || die "VM '$XUI_VM_NAME' is not registered"
    if ! vm_running "$XUI_VM_NAME"; then
        vbox startvm "$XUI_VM_NAME" --type headless
    else
        log "$XUI_VM_NAME is already running"
    fi
}

run_stage_stop() {
    vm_exists "$XUI_VM_NAME" || die "VM '$XUI_VM_NAME' is not registered"
    poweroff_vm "$XUI_VM_NAME"
}

run_stage_status() {
    if ! vm_exists "$XUI_VM_NAME"; then
        log "$XUI_VM_NAME: not registered"
        return
    fi
    local state=stopped
    if vm_running "$XUI_VM_NAME"; then
        state=running
    fi
    local snapshots
    snapshots=$(vbox_quiet snapshot "$XUI_VM_NAME" list --machinereadable 2>/dev/null | grep -c '^SnapshotName=' || true)
    log "$XUI_VM_NAME: $state, expected-ip: $XUI_VM_IP, panel: :$XUI_PANEL_PORT, snapshots: $snapshots"

    local pin_file
    pin_file=$(tunnel_pin_file)
    if [ -s "$pin_file" ]; then
        log "tunnel fixture: host-key pin $(cat "$pin_file") ($pin_file)"
    else
        log "tunnel fixture: no host-key pin recorded — run: $0 provision"
    fi
    if [ "$state" = running ] && [ -f "$KEY_DIR/xui-test-vm-key" ]; then
        if vm_ssh "id $XUI_TUNNEL_USER" >/dev/null 2>&1; then
            log "tunnel fixture: user '$XUI_TUNNEL_USER' present on the VM"
        else
            log "tunnel fixture: user '$XUI_TUNNEL_USER' missing on the VM"
        fi
    fi
    log "note: the snapshot storage of $XUI_VM_NAME lives in $CACHE_DIR — clearing the cache destroys the VM"
}

run_stage_clean() {
    if [ "$ASSUME_YES" != true ]; then
        [ -t 0 ] || die "stdin is not a terminal — pass --yes for non-interactive clean"
        printf 'Delete %s (disk + snapshots)? This cannot be undone. Type the VM name to confirm: ' "$XUI_VM_NAME"
        local answer
        read -r answer
        [ "$answer" = "$XUI_VM_NAME" ] || die "aborted by user"
    fi
    if vm_exists "$XUI_VM_NAME"; then
        poweroff_vm "$XUI_VM_NAME"
        vbox unregistervm "$XUI_VM_NAME" --delete
        log "deleted $XUI_VM_NAME"
    else
        log "$XUI_VM_NAME: not registered"
    fi
}

# --- argument parsing ---------------------------------------------------------------

parse_args() {
    while [ $# -gt 0 ]; do
        case "$1" in
            setup | provision | refresh | reset | start | stop | status | clean)
                [ -z "$STAGE" ] || { printf 'error: multiple stages\n' >&2; exit 1; }
                STAGE=$1 ;;
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
    LOG_FILE="$LOG_DIR/xui-test-vm-$(date +%Y%m%d-%H%M%S).log"

    trap 'status=$?; if [ "$status" -ne 0 ] && [ "$FAILED" = false ]; then
            printf "FAIL: unexpected error (exit %s, log: %s)\n" "$status" "$LOG_FILE" >&2
        fi' EXIT

    log "photon-bypass x-ui test VM — stage: $STAGE, started: $(date '+%Y-%m-%d %H:%M:%S')"

    case "$STAGE" in
        setup) run_stage_setup ;;
        provision) run_stage_provision ;;
        refresh) run_stage_refresh ;;
        reset) run_stage_reset ;;
        start) run_stage_start ;;
        stop) run_stage_stop ;;
        status) run_stage_status ;;
        clean) run_stage_clean ;;
    esac

    log "OK — stage $STAGE finished (log: $LOG_FILE)"
}

main "$@"
