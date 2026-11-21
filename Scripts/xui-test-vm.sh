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
# Image: Debian 12 "genericcloud" qcow2 (pinned snapshot + SHA512 verify) turned
# into a VDI, booted once against a NoCloud seed ISO (cloud-init) for zero-GUI setup.
# x-ui: alireza0 v1.14.0 (pinned release tarball + SHA256 verify).
#
# Stages:
#   setup    download + verify artifacts, build the VM, provision x-ui, create the
#            test inbound, verify the login contract, poweroff and snapshot
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
#   XUI_INBOUND_PORT / XUI_INBOUND_PATH   test inbound        (default 10000 / /testws)
#   DEBIAN_IMAGE_URL / DEBIAN_IMAGE_SHA512  pinned Debian image (defaults below)
#   DEBIAN_IMAGE_PATH               local qcow2 copy instead of downloading (skips verify)
#   XUI_TARBALL_URL / XUI_TARBALL_SHA256    pinned x-ui release (defaults below)
#   XUI_TARBALL_PATH                local tarball copy instead of downloading
#   VBOXMANAGE                      VBoxManage path (default: VBoxManage)
#   VM_CPUS / VM_MEMORY_MB / VM_DISK_MB    VM sizing (default 2 / 1024 / 8192)
#
# Notes:
#   - Downloads honor https_proxy / http_proxy; the local-file *_PATH overrides exist
#     for restricted networks.
#   - The host-only network 192.168.56.1/24 (vboxnet0 or equivalent) is created in
#     preflight when missing — the same network as the Mikrotik test VMs.
#   - The seed ISO embeds the generated SSH public key and the console password; the
#     private key lives in the cache directory (~/.cache/photon-bypass/xui-test-vm),
#     outside the repo, and is reused across runs.
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd -- "$SCRIPT_DIR/.." && pwd)
LOG_DIR="$SCRIPT_DIR/logs"
CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/photon-bypass/xui-test-vm"

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
DEBIAN_IMAGE_URL=${DEBIAN_IMAGE_URL:-"https://cloud.debian.org/images/cloud/bookworm/$DEBIAN_SNAPSHOT/debian-12-genericcloud-amd64-$DEBIAN_SNAPSHOT.qcow2"}
DEBIAN_IMAGE_SHA512=${DEBIAN_IMAGE_SHA512:-946b917a37882f16e5b3b0b2cbe5744637d0cd36190b5b53cbb21d14a54557d5b45e9e523021b8f52c1707dbe470489f3515458e0680ded76ee2574b4a8daf1b}
DEBIAN_IMAGE_PATH=${DEBIAN_IMAGE_PATH:-}
HOSTONLY_SUBNET_IP=${HOSTONLY_SUBNET_IP:-192.168.56.1}
HOSTONLY_NETMASK=${HOSTONLY_NETMASK:-255.255.255.0}
VBOXMANAGE=${VBOXMANAGE:-VBoxManage}
VM_CPUS=${VM_CPUS:-2}
VM_MEMORY_MB=${VM_MEMORY_MB:-1024}
VM_DISK_MB=${VM_DISK_MB:-8192}
SNAPSHOT=${SNAPSHOT:-clean-test-state}
SSH_USER=${SSH_USER:-debian}
SSH_TIMEOUT_SECONDS=${SSH_TIMEOUT_SECONDS:-180}
LOGIN_TIMEOUT_SECONDS=${LOGIN_TIMEOUT_SECONDS:-120}

STAGE=""
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
    if ! "$VBOXMANAGE" "$@" 2>&1 | tee -a "$LOG_FILE"; then
        die "VBoxManage failed: $*"
    fi
}

vbox_quiet() {
    "$VBOXMANAGE" "$@" 2>>"$LOG_FILE"
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

require_tools() {
    command -v "$VBOXMANAGE" >/dev/null 2>&1 || die "VBoxManage not found (env VBOXMANAGE can point at it)"
    command -v qemu-img >/dev/null 2>&1 || die "qemu-img not found (Debian/Ubuntu: apt-get install qemu-utils)"
    command -v jq >/dev/null 2>&1 || die "jq not found (Debian/Ubuntu: apt-get install jq)"
    command -v curl >/dev/null 2>&1 || die "curl not found"
    command -v ssh >/dev/null 2>&1 || die "ssh not found"
    command -v scp >/dev/null 2>&1 || die "scp not found"
    command -v ssh-keygen >/dev/null 2>&1 || die "ssh-keygen not found"
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
    existing=$(vbox_quiet list hostonlyifs | awk -v ip="$HOSTONLY_SUBNET_IP" '
        /^Name:/      { name = $2 }
        /^IPAddress:/ { if ($2 == ip) print name }')
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

# --- artifacts --------------------------------------------------------------------

download_file() { # $1=url, $2=destination, $3=reason
    [ -f "$2" ] && return
    note "downloading $3 (honors https_proxy)"
    curl -fsSL --retry 3 --output "$2.part" "$1" || die "download failed: $1"
    mv "$2.part" "$2"
}

debian_qcow2() { # prints the path of the pinned Debian cloud image
    local target="$CACHE_DIR/debian-12-genericcloud-amd64-$DEBIAN_SNAPSHOT.qcow2"

    if [ -n "$DEBIAN_IMAGE_PATH" ]; then
        [ -f "$DEBIAN_IMAGE_PATH" ] || die "DEBIAN_IMAGE_PATH not found: $DEBIAN_IMAGE_PATH"
        printf '%s\n' "$DEBIAN_IMAGE_PATH"
        return
    fi

    download_file "$DEBIAN_IMAGE_URL" "$target" "Debian $DEBIAN_SNAPSHOT genericcloud image"
    printf '%s  %s\n' "$DEBIAN_IMAGE_SHA512" "$target" | sha512sum -c - \
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
    printf '%s  %s\n' "$XUI_TARBALL_SHA256" "$target" | sha256sum -c - \
        || die "x-ui tarball checksum mismatch — update XUI_TARBALL_SHA256 or use XUI_TARBALL_PATH"
    printf '%s\n' "$target"
}

ensure_ssh_key() {
    local key="$CACHE_DIR/xui-test-vm-key"
    if [ ! -f "$key" ]; then
        note "generating SSH keypair for the VM (kept in the cache dir)"
        ssh-keygen -q -t ed25519 -N '' -f "$key"
    fi
    printf '%s\n' "$key"
}

# --- seed ISO ---------------------------------------------------------------------

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

    cat >"$seed_dir/network-config" <<EOF
version: 2
ethernets:
  nic-nat:
    match:
      macaddress: "$3"
    set-name: nic-nat
    dhcp4: true
  nic-hostonly:
    match:
      macaddress: "$4"
    set-name: nic-hostonly
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

vm_ssh() { # rest = remote command
    ssh -i "$CACHE_DIR/xui-test-vm-key" -o IdentitiesOnly=yes \
        -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \
        -o ConnectTimeout=10 "$SSH_USER@$XUI_VM_IP" "$@"
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
    curl -s --max-time 10 -c "$CACHE_DIR/panel.cookies" \
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
    curl -s --max-time 15 -b "$CACHE_DIR/panel.cookies" \
        -H 'Content-Type: application/json' -H 'X-Requested-With: XMLHttpRequest' \
        -d "$2" "http://$XUI_VM_IP:$XUI_PANEL_PORT/$1"
}

create_test_inbound() { # prints the created inbound id
    local settings stream payload response
    settings=$(jq -nc '{clients: [], decryption: "none"}')
    stream=$(jq -nc --arg path "$XUI_INBOUND_PATH" \
        '{network: "ws", security: "none", wsSettings: {path: $path}}')
    payload=$(jq -nc \
        --argjson port "$XUI_INBOUND_PORT" \
        --arg settings "$settings" \
        --arg stream "$stream" \
        '{up: 0, down: 0, total: 0, remark: "test-inbound", enable: true, expiryTime: 0,
          listen: "0.0.0.0", port: $port, protocol: "vless",
          settings: $settings, streamSettings: $stream,
          sniffing: {enabled: true, destOverride: ["http", "tls", "quic"]}}')

    note "creating the test inbound (VLESS+ws, port $XUI_INBOUND_PORT, path $XUI_INBOUND_PATH)"
    response=$(panel_post 'xui/API/inbounds/add' "$payload")
    [ "$(printf '%s' "$response" | jq -r '.success // empty' 2>/dev/null || true)" = "true" ] \
        || die "inbound add failed: $(printf '%s' "$response" | jq -c '{success, msg}' 2>/dev/null || printf 'unparseable response')"

    local list inbound_id
    list=$(curl -s --max-time 15 -b "$CACHE_DIR/panel.cookies" \
        -H 'X-Requested-With: XMLHttpRequest' \
        "http://$XUI_VM_IP:$XUI_PANEL_PORT/xui/API/inbounds/")
    inbound_id=$(printf '%s' "$list" | jq -r --argjson port "$XUI_INBOUND_PORT" \
        '.obj[] | select(.port == $port) | .id' 2>/dev/null | head -n 1)
    [ -n "$inbound_id" ] || die "could not read back the created inbound id (port $XUI_INBOUND_PORT)"
    note "inbound created: id=$inbound_id"
    printf '%s\n' "$inbound_id"
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

# --- stages -----------------------------------------------------------------------

run_stage_setup() {
    local hostonly_if seed_iso ssh_key tarball disk mac_nat mac_hostonly

    require_tools
    abort_if_registered
    hostonly_if=$(ensure_hostonly_network)
    ssh_key=$(ensure_ssh_key)
    tarball=$(xui_tarball)
    disk="$CACHE_DIR/$XUI_VM_NAME-disk.vdi"

    log "converting the Debian image to VDI and resizing to ${VM_DISK_MB}MB"
    rm -f "$disk"
    qemu-img convert -f qcow2 -O vdi "$(debian_qcow2)" "$disk"
    vbox modifymedium disk "$disk" --resize "$VM_DISK_MB"

    log "creating VM $XUI_VM_NAME ($XUI_VM_IP)"
    vbox createvm --name "$XUI_VM_NAME" --ostype Debian_64 --register
    vbox modifyvm "$XUI_VM_NAME" --cpus "$VM_CPUS" --memory "$VM_MEMORY_MB" --vram 16 --ioapic on \
        --nic1 nat --nictype1 virtio \
        --nic2 hostonly --nictype2 virtio --hostonlyadapter2 "$hostonly_if"
    vbox storagectl "$XUI_VM_NAME" --name SATA --add sata --bootable on
    vbox storagectl "$XUI_VM_NAME" --name IDE --add ide
    vbox storageattach "$XUI_VM_NAME" --storagectl SATA --port 0 --device 0 --type hdd --medium "$disk"

    mac_nat=$(vbox_quiet showvminfo "$XUI_VM_NAME" --machinereadable | awk -F'"' '/^macaddress1=/{print $2}')
    mac_hostonly=$(vbox_quiet showvminfo "$XUI_VM_NAME" --machinereadable | awk -F'"' '/^macaddress2=/{print $2}')
    [ -n "$mac_nat" ] && [ -n "$mac_hostonly" ] || die "could not read the NIC MAC addresses"

    seed_iso=$(build_seed_iso "$XUI_VM_NAME" "$ssh_key" "$mac_nat" "$mac_hostonly")
    vbox storageattach "$XUI_VM_NAME" --storagectl IDE --port 1 --device 0 --type dvddrive --medium "$seed_iso"

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

    log "powering off for the snapshot"
    vm_ssh 'sudo systemctl poweroff' >/dev/null 2>&1 || true
    if ! wait_state_off "$XUI_VM_NAME" 90; then
        log "graceful poweroff timed out — hard poweroff"
        poweroff_vm "$XUI_VM_NAME"
    fi
    vbox storageattach "$XUI_VM_NAME" --storagectl IDE --port 1 --device 0 --type dvddrive --medium emptydrive
    vbox snapshot "$XUI_VM_NAME" take "$SNAPSHOT"
    log "snapshot '$SNAPSHOT' taken"

    cat <<EOF

OK — x-ui test VM ready.

  panel:      http://$XUI_VM_IP:$XUI_PANEL_PORT  ($XUI_PANEL_USER / $XUI_PANEL_PASSWORD)
  inbound:    id=$inbound_id  VLESS+ws  port $XUI_INBOUND_PORT  path $XUI_INBOUND_PATH
  ssh:        $SSH_USER@$XUI_VM_IP with key $CACHE_DIR/xui-test-vm-key
  console:    VirtualBox console, $SSH_USER / XUI_CONSOLE_PASSWORD (env at setup time)

TestVmOptions snippet for Backend/Tests/appsettings.json:

  "Xui-Test": { "Ip": "$XUI_VM_IP", "Port": $XUI_PANEL_PORT, "Username": "$XUI_PANEL_USER", "Password": "$XUI_PANEL_PASSWORD", "InboundId": $inbound_id }

Reset semantics: '$SNAPSHOT' — restore per test run (the future XuiPanelInitializer will do this automatically).
EOF
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
            setup | reset | start | stop | status | clean)
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
        reset) run_stage_reset ;;
        start) run_stage_start ;;
        stop) run_stage_stop ;;
        status) run_stage_status ;;
        clean) run_stage_clean ;;
    esac

    log "OK — stage $STAGE finished (log: $LOG_FILE)"
}

main "$@"
