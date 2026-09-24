# Intent: Require client certificates (mTLS) through the nginx reverse proxy
Author: chitza. Issue: #30. Status: draft.

## Problem
The app has no authentication. Anyone who can reach the UI can call every API, including
`POST /api/files/delete` on read-write mounts, scan import and the SignalR hub.
The operator already runs nginx in front of all their containers, and wants only devices
that hold a client certificate issued by their own CA to reach WebDiskTree.

## Proposed outcome
- The operator's nginx terminates TLS for WebDiskTree and requires a client certificate
  signed by a private CA (`ssl_verify_client on`). A request without a valid certificate is
  rejected by nginx and never reaches the app.
- The repo documents a working setup: an example nginx server block, openssl commands to create
  the CA and a client certificate (`.p12` for browsers), and how to revoke a client.
- The example config carries everything the app needs through the proxy: the SignalR WebSocket upgrade on
  `/hubs/`, a request body limit that allows the 100 MiB scan import, and timeouts long enough for SignalR.
- The app container's HTTP port is no longer reachable around nginx: `docker-compose.yml` stops publishing
  `8080` on all host interfaces.
- The app itself gets no authentication code.

## Affected parts
Docker/compose (`docker-compose.yml`), docs (`README.md`, new example nginx config).
No backend, frontend, EF schema or CI change.

## Constraints
- Security depends on the deployment: anyone who can reach the container's port 8080 directly bypasses mTLS.
  The docs must say this plainly.
- The operator's nginx is outside this repo. The repo ships an example, not their live config.
- Local development (`dotnet run` + `ng serve`) stays plain HTTP with no certificates.
- `docker run -p 8080:8080` in the README still works for someone without a proxy; it is documented as unauthenticated.
- Out of scope: app-level certificate auth, per-certificate permissions (for example delete only for some certs),
  passing the client identity to the app, and any change to the operator's live nginx.

## Open questions
1. **How does your nginx reach the container?** (a) nginx runs on the host, so compose should publish
   `127.0.0.1:8080:8080`; (b) nginx is a container, so compose should join a shared external Docker network
   and publish no port; (c) something else. This decides the `docker-compose.yml` change and the `proxy_pass` target.
2. **Revocation.** Is removing a device by rotating the CA enough, or should the example include a CRL
   (`ssl_crl`) and the commands to revoke one client certificate?
3. **Where should the example config live?** Proposed: `deploy/nginx/webdisktree.conf`, referenced from a new
   "Client-certificate access (mTLS)" section in `README.md`.
4. **Smoke run.** Should the proof include a throwaway nginx container with test certificates
   (curl with no cert is rejected, curl with a cert gets 200 from `GET /api/scans`, the hub negotiate succeeds),
   or is checking the example config with `nginx -t` enough?
