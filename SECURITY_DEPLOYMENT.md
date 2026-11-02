# Security deployment checklist

The repository intentionally does not contain production credentials. Configure these values in the hosting environment or secret manager before starting the API:

- `ConnectionStrings__HSCrmConnectionString`: SQL Server connection string.
- `Jwt__Key`: a cryptographically random signing key containing at least 32 UTF-8 bytes. Treat any previously committed key as compromised and rotate it.
- `Jwt__Issuer` and `Jwt__Audience`: non-empty values matching the API token issuer and intended client audience.
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...: exact browser origins allowed to call the API (scheme and host, plus port when applicable). Do not use wildcard origins.

The API now requires an authenticated user by default. Only endpoints explicitly marked with `[AllowAnonymous]` remain public: tenant registration, credential check, login, and the limited fiscal-year dropdown. Other API calls require a valid JWT; endpoints marked with `[Permission]` additionally require that permission.

The user-creation endpoint now requires a per-user `PasswordC` field. Do not reuse a shared/default password.

The API no longer trusts the `X-FiscalYear-Id` request header. The active fiscal year is read from the validated JWT claim. A client must obtain a token containing the selected, server-validated fiscal-year claim before calling fiscal-year-scoped endpoints.

Image uploads require the user-edit permission and are limited to five PNG/JPEG images, each at most 5 MiB. The API checks PNG/JPEG structure headers and dimensions (maximum 4096 × 4096 and 16 million pixels), stores new files under `App_Data/UserImages` outside `wwwroot`, and serves them through a constrained image endpoint with `nosniff`. This is not a full decoder/re-encoder pipeline; malformed image payloads and image-library parser risk still require a final review. The `App_Data/UserImages` directory must be writable by the API process, persistent across deployments, and included in backups. Existing files already under `wwwroot/upload/userimage` remain served by static-file middleware until migrated.

## Required operational actions

1. Rotate the SQL Server credential and JWT signing key. New JWTs include the Identity security stamp; authenticated requests re-check the stamp, user status, tenant status, and fiscal-year status. Clients must sign in again after deployment because tokens without the new security-stamp claim are rejected.
2. Set the environment variables above in every environment before deployment. The IP-based rate limits rely on `RemoteIpAddress`; when running behind a reverse proxy, configure trusted forwarded headers at the hosting layer so limits are applied per client IP (do not trust arbitrary forwarded headers).
3. Configure the exact frontend origins in CORS.
4. Review the existing Git history and repository forks/clones. The current change removes secrets from the latest tracked settings file but does not rewrite Git history.
5. Run the full .NET build and unit/integration test suite in CI before merging.
