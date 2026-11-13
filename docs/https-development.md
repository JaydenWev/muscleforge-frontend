# Android HTTPS development

Settings accepts an HTTPS server root (or proxy prefix), without `/api/v1`. The URL's host must resolve on the device, reach the listener, and match a subject alternative name (SAN) in a valid certificate chain. These are separate requirements. A physical phone needs a reachable LAN or VPN address; `localhost` refers to the phone itself. The Android app disables cleartext; do not bypass certificate or hostname validation. Native requests do not need a CORS exception. Development and acceptance use physical phones.

The backend SDK `https` profile serves `https://localhost:7243` and `http://localhost:5187`; its usual development certificate for localhost alone does not establish trust or identity for a reachable phone address. Compose and the Visual Studio Docker profile expose HTTP on host loopback `127.0.0.1:5187`, not HTTPS. They need a TLS terminator accessible to the device. Do not expose that HTTP port to the network. Follow the backend README for database, migration, owner, and startup prerequisites; a status response alone does not establish database readiness or exercise endpoint availability.

## Private CA and SDK listener

This development-only example creates a private CA and a server certificate using OpenSSL, then runs Kestrel on a separate port. Use a private directory outside both repositories, protect and back up the CA key as appropriate, and never commit or share private keys or passwords. Anyone possessing the CA private key can issue certificates trusted by devices on which it is installed. Use a dedicated development CA, not an existing sensitive CA. These commands require OpenSSL and a POSIX shell. They create new files; choose an empty directory and do not overwrite an existing CA.

Replace `dev-server` and `192.168.1.20` in the SAN line with the actual reachable DNS name and IP if needed. Include only addresses you intend to use. A DNS SAN is required for a hostname and an IP SAN for a literal IP; resolving a short name does not establish certificate trust. The example server must be reachable from the phone on the chosen interface and port, including firewall/VPN policy. The certificate's SANs do not configure DNS or network routes.

```bash
(
set -eu
umask 077
mkdir -m 700 "$HOME/muscleforge-dev-ca"
cd "$HOME/muscleforge-dev-ca"
openssl genrsa -out ca.key 4096
openssl req -x509 -new -key ca.key -sha256 -days 3650 \
  -subj '/CN=MuscleForge Development CA' \
  -addext 'basicConstraints=critical,CA:TRUE,pathlen:0' \
  -addext 'keyUsage=critical,keyCertSign,cRLSign' -out ca.crt
openssl genrsa -out server.key 2048
openssl req -new -key server.key -subj '/CN=dev-server' -out server.csr
cat > server.ext <<'EXT'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:localhost,DNS:dev-server,IP:127.0.0.1,IP:192.168.1.20
EXT
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -out server.crt -days 365 -sha256 -extfile server.ext
openssl verify -CAfile ca.crt server.crt
openssl x509 -in server.crt -noout -ext subjectAltName
openssl pkcs12 -export -out server.pfx -inkey server.key -in server.crt -certfile ca.crt
openssl x509 -in ca.crt -outform DER -out ca.cer
)
```

Choose a strong PFX password at the prompt. Transfer only `ca.cer` to Android through a trusted channel, never `ca.key`, `server.key`, or `server.pfx`. Reissue the leaf when its SANs change or it expires; replace and remove the CA on devices if it is compromised or retired.

Stop any conflicting listener and check the chosen port before starting. For SDK development, from the backend checkout with the README's prerequisites configured, run the following in a private shell. This replaces the launch profile for this process and listens on all interfaces on port 7443; restrict network access with host firewall/VPN rules. It does not modify an existing Compose or Visual Studio service. The password is an environment variable in this process; avoid shell history, process dumps, and logs containing it. If port 7443 is in use, choose another unused port throughout.

```bash
read -r -s -p 'Server PFX password: ' KESTREL_PFX_PASSWORD; printf '\n'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS='https://0.0.0.0:7443'
export Kestrel__Certificates__Default__Path="$HOME/muscleforge-dev-ca/server.pfx"
export Kestrel__Certificates__Default__Password="$KESTREL_PFX_PASSWORD"
dotnet run --project src/MuscleForge.Api --no-launch-profile
unset KESTREL_PFX_PASSWORD Kestrel__Certificates__Default__Password
```

The server PFX here is a TLS identity. The backend's separate Data Protection PFX protects authentication keys at rest; it is not an HTTPS certificate and must not be substituted. Test the chain and exact hostname from a machine that can reach it, without disabling verification:

```bash
curl --fail --cacert "$HOME/muscleforge-dev-ca/ca.crt" https://dev-server:7443/api/v1/status
```

Use `https://dev-server:7443` (or the included reachable IP) in phone Settings. Install the CA first as described below. The server name must actually resolve on the phone; test reachability separately.

## Android trust and installation

Build and install the signed **Debug** APK using the frontend README. Transfer `ca.cer` to the phone, then use Android Settings to install it as a **CA certificate** (typically Security & privacy → More security settings → Encryption & credentials → Install a certificate → CA certificate; labels vary by Android version/vendor). Confirm the warning and device screen lock if prompted. Remove the transferred file after installation. The app's network security configuration trusts system CAs and, only when debuggable, user-installed CAs. Hostname and chain validation still apply. Release builds trust system CAs only; installing this user CA does not make this private chain trusted by Release. Remove the development CA via Settings → trusted credentials/user credentials when finished or compromised; remove it on every device where installed. Removing a CA affects other apps that trust it too.

For wireless installation on Android 11+, connect computer and phone to the same Wi-Fi, enable Developer options and Wireless debugging, then choose **Pair device with pairing code**. Run `adb pair <phone-ip>:<pairing-port>` and enter the displayed code. If the device does not connect automatically, use the separate IP/port shown on the main Wireless debugging screen: `adb connect <phone-ip>:<connection-port>`. Check `adb devices`, then `adb -s <serial> install -r /path/to/local.muscleforge.app-Signed.apk`. Pairing and connection ports can differ; network isolation may block discovery. Disable wireless debugging when done. On older Android, use USB debugging instead. See [Android's device connection instructions](https://developer.android.com/studio/run/device).

## Compose with the private CA

For Compose, the private certificate can also terminate TLS in the existing API container. This is an alternative to the SDK listener and the Tailscale route below. Stop any conflicting listener and choose an unused host port and a specific host LAN or VPN IP that the phone can reach; restrict it with the host firewall. The certificate SAN must include the exact hostname or IP used in Settings. A bare hostname such as `dev-server` needs a matching DNS SAN and working name resolution on the phone. Do not change unrelated services.

Keep the encrypted `server.pfx` from the CA recipe in its private directory, readable by container UID 1654. On a typical rootful Linux Docker host, `chmod 0644 "$HOME/muscleforge-dev-ca/server.pfx"` makes the mounted file readable while the containing directory remains mode 0700. Check the host's permissions and Docker/SELinux or rootless configuration as applicable. The CA private key must remain private. Use a strong PFX password. This file and password are separate from Data Protection's PFX and password.

Create a local override outside both repositories, for example `~/muscleforge-dev-ca/compose.https.yaml`:

```yaml
services:
  api:
    environment:
      Kestrel__Endpoints__Http__Url: http://0.0.0.0:8080
      Kestrel__Endpoints__Https__Url: https://0.0.0.0:7443
      Kestrel__Endpoints__Https__Certificate__Path: /run/secrets/dev-tls.pfx
      Kestrel__Endpoints__Https__Certificate__Password: ${DEV_TLS_PASSWORD:?Set DEV_TLS_PASSWORD}
    ports:
      - "${DEV_TLS_BIND_IP:?Set DEV_TLS_BIND_IP}:7443:7443"
    volumes:
      - type: bind
        source: ${DEV_TLS_PFX_FILE:?Set DEV_TLS_PFX_FILE to an absolute path}
        target: /run/secrets/dev-tls.pfx
        read_only: true
```

Add `DEV_TLS_BIND_IP`, `DEV_TLS_PFX_FILE` (an absolute path to `server.pfx`) and `DEV_TLS_PASSWORD` to the backend's ignored local `.env`; use the [Compose environment-file quoting rules](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/#env-file-syntax) for special characters and restrict access to the file. Never commit these values or include them in logs. The backend's existing values and Data Protection configuration remain necessary. From the backend checkout, with its prerequisites and migrations already complete, apply the original Compose file first and the override second:

```bash
docker compose --env-file .env -f compose.yaml -f "$HOME/muscleforge-dev-ca/compose.https.yaml" up -d --no-deps api
```

This recreates the MuscleForge API container with both HTTP 8080 inside the container (still published on host loopback 5187 by the original file) and HTTPS 7443 on the specified host address. It does not configure Visual Studio's independently managed Docker profile; use a separately managed TLS proxy for that route. Use `https://dev-server:7443` in phone Settings after installing the CA. Verify the listener, certificate chain and SAN, and endpoint as above. To return to the original Compose configuration, recreate the API using the original file alone; do not remove volumes. This is a development setup, not production deployment guidance.

## Docker or Visual Studio Docker: optional Tailscale HTTPS proxy

The Docker and Visual Studio Docker routes leave the backend on host loopback HTTP 5187. One optional TLS terminator is Tailscale Serve, provided the server and phone are on the same tailnet and HTTPS certificates are enabled for that tailnet. First inspect `tailscale serve status`, the node's full MagicDNS name, port use, and tailnet access policy. Existing services may already occupy ports or paths: pick an unused HTTPS port and do not reset or overwrite their configuration. Verify the intended local upstream is the API at `http://127.0.0.1:5187/api/v1/status`. After checking the current [Serve command syntax](https://tailscale.com/docs/reference/tailscale-cli/serve), an example for an unused port is:

```bash
tailscale serve --bg --https=8443 http://127.0.0.1:5187
tailscale serve status
```

Use `https://<full-node-name>.<tailnet>.ts.net:8443` in Settings, with the actual full node FQDN shown by Tailscale, and keep Tailscale connected on the phone. Serve provisions its TLS certificate for that full name; a bare short hostname does not match a `*.ts.net` certificate, even if it resolves to the same machine. Public CA trust still depends on the device's system trust store. A tailnet hostname or existing unrelated service does not prove this API route or certificate is configured. Remove only the specific route created for this setup using the documented per-port `off` command after checking status; do not use a global reset. Serve availability and policy are environment-specific. The private-CA Kestrel route above is an independent SDK option.

## Acceptance evidence

Record actual results on the owner's physical Android phone; this guide does not claim device verification. Record date, frontend commit, exact APK path and SHA-256, Debug/Release and effective network security configuration, phone model and Android version/API, backend commit and startup route, exact endpoint host/port, server certificate SAN/issuer and CA trust state (never secrets), plus the observed Settings outcome. Use a controlled endpoint for each case:

| Case | Expected Settings result |
| --- | --- |
| Debug, matching host and installed issuing CA | Save & connect succeeds when API is reachable. |
| Debug, issuing CA absent or removed | Connection fails; no success state. |
| Debug, trusted chain but URL host absent from SAN | Connection fails; no success state. |
| HTTP URL | Rejected; no cleartext request succeeds. |
| Release, private user CA only | Connection fails; user CA is not a Release trust anchor. |

Restore the trusted setup after negative cases and record the observed result, including errors and environment limitations. Confirm `/health/ready` and the required exercise endpoint separately before claiming full backend availability. See [Android network security configuration](https://developer.android.com/privacy-and-security/security-config) and [Kestrel endpoint and certificate configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0).
