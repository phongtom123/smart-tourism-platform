# Smart Tourism Platform

Public source snapshot for a smart tourism system built with .NET MAUI, ASP.NET Core, and a PHP/XAMPP admin portal.

This repository is intended for showcase and review. Private credentials, database dumps, local payloads, deployment secrets, keystores, and collaboration history are not included.

## Main Modules

- `C-SA-T/`: .NET MAUI mobile app for visitors.
- `VinhKhanh/VinhKhanh/`: ASP.NET Core backend API.
- `CS_admin/`: PHP admin portal for XAMPP.
- `C-SA-T.Tests/` and `VinhKhanh/VinhKhanh.Tests/`: unit tests.
- `docs/`: PRD, diagrams, testcase documents, and project notes.
- `database/`: public migration and helper SQL scripts only.
- `sandbox/`, `tools/`, `tests/`: supporting test utilities and scripts.

## Project Structure

```text
smart-tourism-platform/
├── C-SA-T/                         # .NET MAUI mobile application
│   ├── Controls/                   # Reusable UI controls
│   ├── Models/                     # Mobile data models
│   ├── Platforms/                  # Android and Windows platform code
│   ├── Resources/                  # Images, fonts, app icon, splash, raw assets
│   ├── Secret/                     # Example secret templates only
│   ├── Services/                   # API, cache, geofence, tour, audio, and device services
│   ├── Utils/                      # Shared helpers
│   └── Views/                      # App screens and map pages
├── C-SA-T.Tests/                   # Unit tests for mobile rules and logic
├── CS_admin/                       # PHP/XAMPP admin portal
│   ├── admin/                      # Admin pages
│   ├── api/                        # PHP API/proxy endpoints
│   ├── asset/admin/                # Admin CSS, JS, and components
│   └── Secret/                     # Example browser-key template only
├── VinhKhanh/
│   ├── VinhKhanh/                  # ASP.NET Core backend API
│   │   ├── Controllers/            # API controllers
│   │   ├── Data/                   # Database context
│   │   ├── Database/               # Public schema and migrations
│   │   ├── Dtos/                   # Request/response DTOs
│   │   ├── Middleware/             # Request/device middleware
│   │   ├── Services/               # Business logic services
│   │   └── wwwroot/                # Public static assets
│   ├── VinhKhanh.Tests/            # Backend unit tests
│   └── Quan4TestSuite/             # Integration/load-style test utilities
├── database/                       # Public SQL migrations and helper scripts
├── docs/                           # PRD, diagrams, reports, and test documents
├── sandbox/                        # Experimental runners and simulations
├── tests/                          # Script-based test runners
└── tools/                          # Local helper scripts
```

## Features

- QR access flow and package registration.
- Geofence-based guide logic and audio playback.
- Tour, store, food, invoice, account, and device management.
- Admin dashboard and POI map workflows.
- Backend service and queue logic covered by unit tests.

## Local Configuration

Do not commit real secrets to this public repository.

1. Backend: copy `VinhKhanh/VinhKhanh/appsettings.Development.example.json` to `appsettings.Development.json`, then fill in your local connection string, PayOS, SMTP, and bank information.
2. Mobile: copy `C-SA-T/Secret/props.example.txt` to `C-SA-T/Secret/props.txt`, then fill in your Google Maps API key.
3. Admin PHP: if Google Maps is needed in the admin portal, create `CS_admin/Secret/google-maps-browser-key.txt` from the example file.

## Tests

```powershell
dotnet test C-SA-T.Tests\C-SA-T.Tests.csproj
dotnet test VinhKhanh\VinhKhanh.Tests\VinhKhanh.Tests.csproj
```
