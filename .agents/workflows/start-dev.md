---
description: Start the full development environment (backend + frontend)
---

# Start Development Environment

Starts both the .NET backend API and the Angular frontend dev server in parallel.

// turbo-all

## Steps

1. Run the start script from the project root:
```powershell
.\start.ps1
```

## Alternative: Start individually

### Backend only
```powershell
.\start.ps1 -Backend
```

### Frontend only
```powershell
.\start.ps1 -Frontend
```

## Ports
- **Backend (.NET)**: Check `Properties/launchSettings.json` or `appsettings.json` for the configured port
- **Frontend (Angular)**: Default is `http://localhost:4200`
