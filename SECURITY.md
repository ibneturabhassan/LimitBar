# Privacy and security

LimitBar runs locally and has no telemetry, analytics, hosted backend, or separate user account.

## Account access

- Codex requests go through the locally installed Codex CLI and its signed-in app-server protocol.
- Claude's existing access token is read from its local credentials file and sent only to `https://api.anthropic.com/api/oauth/usage`. HTTP redirects are disabled.
- Claude uses an internal, unofficial endpoint. Its behavior or availability can change.
- LimitBar does not write or log provider tokens. It stores UI preferences in `%LOCALAPPDATA%\LimitBar\settings.json`.
- A successful reading is cached in memory so a failed refresh can be labeled stale. There is no on-disk usage history.
- Automatic startup is opt-in. Screenshots and offline checks use fictional readings and do not read credentials.

Network access to provider services is still required. Provider software has its own privacy behavior. The lack of a LimitBar server does not mean every request is offline.

## Report a vulnerability

Use GitHub's private [Report a vulnerability](https://github.com/ibneturabhassan/LimitBar/security/advisories/new) flow when available. Do not post tokens, credentials, authenticated response bodies, or private account information in public issues. If private reporting is unavailable, open a public issue asking for a private contact without revealing exploit details.

The project is maintained on a best-effort basis. The newest release is the supported version.

## Release integrity

Community builds are initially unsigned. Download binaries from this repository's Releases page and compare the provided SHA-256 hashes. Hashes detect mismatched files; they do not replace code signing. Do not disable Windows protection to install the app.
