# Security deployment checklist

The repository intentionally does not contain production credentials. Configure these values in the hosting environment or secret manager before starting the API:

- `ConnectionStrings__HSCrmConnectionString`: SQL Server connection string.
- `Jwt__Key`: a cryptographically random signing key containing at least 32 UTF-8 bytes. Treat any previously committed key as compromised and rotate it.
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...: exact browser origins allowed to call the API (scheme and host, plus port when applicable). Do not use wildcard origins.

The API now requires an authenticated user by default. Only endpoints explicitly marked with `[AllowAnonymous]` remain public: tenant registration, credential check, login, and the limited fiscal-year dropdown. Other API calls require a valid JWT; endpoints marked with `[Permission]` additionally require that permission.

The user-creation endpoint now requires a per-user `PasswordC` field. Do not reuse a shared/default password.

The API no longer trusts the `X-FiscalYear-Id` request header. The active fiscal year is read from the validated JWT claim. A client must obtain a token containing the selected, server-validated fiscal-year claim before calling fiscal-year-scoped endpoints.

Image uploads require the user-edit permission and are limited to five PNG/JPEG images, each at most 5 MiB, with a matching file signature.

## Required operational actions

1. Rotate the SQL Server credential and JWT signing key; changing this repository does not revoke already-issued tokens until the signing key is rotated.
2. Set the environment variables above in every environment before deployment.
3. Configure the exact frontend origins in CORS.
4. Review the existing Git history and repository forks/clones. The current change removes secrets from the latest tracked settings file but does not rewrite Git history.
5. Run the full .NET build and unit/integration test suite in CI before merging.
