# ClinicalHealthcare — Deployment Runbook

## 1. Angular SPA — Netlify

### Prerequisites

- Node 20 LTS, npm 10+
- Netlify CLI or GitHub Actions with `NETLIFY_AUTH_TOKEN` + `NETLIFY_SITE_ID` secrets

### Build & Deploy

```bash
cd clinical-hub
npm ci
npx ng build --configuration production
# Output: clinical-hub/dist/clinical-hub/browser/
```

`netlify.toml` at the repo root of `clinical-hub/` handles the SPA fallback:

```toml
[[redirects]]
  from   = "/*"
  to     = "/index.html"
  status = 200
```

All Angular deep links work because Netlify serves `index.html` for every unmatched path.

---

## 2. ASP.NET Core API — IIS (Windows)

### Prerequisites

- Windows Server 2019/2022 with IIS 10+
- [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) installed (installs ASP.NET Core Module v2)
- Application pool: **No Managed Code** (in-process module manages the runtime)

### Publish

```bash
dotnet publish src/ClinicalHealthcare.Api \
  --configuration Release \
  --output C:\inetpub\clinicalhealthcare\api
```

Verify `web.config` is present in the publish output:

```bash
Test-Path "C:\inetpub\clinicalhealthcare\api\web.config"  # must return True
```

### IIS Site Setup

1. Open **IIS Manager** → **Sites** → **Add Website**
   - Site name: `ClinicalHealthcare`
   - Physical path: `C:\inetpub\clinicalhealthcare\api`
   - Binding: HTTPS, port 443 (see TLS section below)

2. **Application Pool** → **No Managed Code**

3. Set environment variables (do NOT hardcode in `web.config` — AC-005):
   - IIS Manager → Application Pools → `[pool name]` → Advanced Settings → **Environment Variables**
   - Required variables:

     | Variable | Description |
     |----------|-------------|
     | `ASPNETCORE_ENVIRONMENT` | `Production` |
     | `SQLSERVER_CONNECTION_STRING` | Full SQL Server connection string |
     | `POSTGRES_CONNECTION_STRING` | Full PostgreSQL connection string |
     | `REDIS_CONNECTION_STRING` | Upstash/Redis connection string |
     | `SEQ_SERVER_URL` | Seq CE ingestion URL, e.g. `http://seq-host:5341` |
     | `SEQ_API_KEY` | Seq API key (optional; leave unset for anonymous ingestion on private networks) |

### TLS Binding (AC-004)

1. Obtain a certificate (Let's Encrypt via `win-acme`, or a CA-issued PFX).
2. Import to **Local Machine → Personal** certificate store.
3. In IIS Manager → Site Bindings → **Add**:
   - Type: `https`
   - Port: `443`
   - SSL Certificate: select imported cert
4. **Enforce HTTPS** — add an HTTP binding (port 80) and a URL Rewrite rule for 301 redirect:

```xml
<!-- Insert inside <system.webServer> in web.config only on the HTTP site -->
<rewrite>
  <rules>
    <rule name="HTTP to HTTPS" stopProcessing="true">
      <match url="(.*)" />
      <conditions>
        <add input="{HTTPS}" pattern="^OFF$" />
      </conditions>
      <action type="Redirect"
              url="https://{HTTP_HOST}/{R:1}"
              redirectType="Permanent" />
    </rule>
  </rules>
</rewrite>
```

> `UseHttpsRedirection()` in `Program.cs` handles application-level redirect; the IIS rule handles the port-80 listener before ASP.NET Core processes the request.

---

## 3. Windows Service Hosting

`Program.cs` calls `builder.Host.UseWindowsService()` which is a no-op when not running as a service — safe for development and IIS deployments.

### Install as a Windows Service

```powershell
# Publish to a permanent path
dotnet publish src/ClinicalHealthcare.Api `
  --configuration Release `
  --output "C:\Services\ClinicalHealthcare"

# Create the service
sc.exe create "ClinicalHealthcareApi" `
  binPath= "C:\Services\ClinicalHealthcare\ClinicalHealthcare.Api.exe" `
  start= auto `
  obj= "NT AUTHORITY\NetworkService"

# Set environment variables for the service account
[System.Environment]::SetEnvironmentVariable(
  "SQLSERVER_CONNECTION_STRING", "<value>",
  [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable(
  "POSTGRES_CONNECTION_STRING", "<value>",
  [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable(
  "REDIS_CONNECTION_STRING", "<value>",
  [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable(
  "ASPNETCORE_ENVIRONMENT", "Production",
  [System.EnvironmentVariableTarget]::Machine)

# Start the service
sc.exe start "ClinicalHealthcareApi"
```

### Uninstall

```powershell
sc.exe stop "ClinicalHealthcareApi"
sc.exe delete "ClinicalHealthcareApi"
```

---

## 4. Secrets Policy (AC-005)

- All connection strings and API keys are read **exclusively from environment variables** at startup.
- No secrets appear in `appsettings.json`, `appsettings.Production.json`, or `web.config`.
- `appsettings.*.local.json` overrides are covered by `.gitignore` and must never be committed.
- Verify before any commit:

```bash
# Must return 0 results
grep -rn "Password=" src/ clinical-hub/src/ --include="*.json" --include="*.config"
```

---

## 5. Health Check Verification

After deployment, verify the API is running:

```bash
curl https://api.clinicalhub.app/health
# Expected: {"status":"Healthy"} or {"status":"Degraded"} (Redis unreachable)
```

A `Degraded` response means the API is functional but Redis is offline. Investigate Redis connectivity but do not treat it as a deployment failure.
