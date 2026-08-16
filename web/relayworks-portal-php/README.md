# RelayWorks Modern PHP Portal

A lightweight, modern, server-side-rendered PHP web portal for the RelayWorks Integration Platform. Built with modern vanilla PHP 8.2+ (strict types, readonly classes, PSR-4 routing, clean zero-dependency design).

---

## 📁 Directory Structure

```text
web/relayworks-portal-php/
├── public/                 # Document root
│   ├── index.php           # Front controller & route dispatcher
│   ├── css/style.css       # Clean dark-mode dashboard stylesheet
│   └── js/portal.js        # Minimal client-side interactivity
├── src/
│   ├── Config.php          # Environment & tenant configuration loader
│   ├── ApiClient.php       # cURL HTTP client connecting to Control Plane API
│   ├── Router.php          # Zero-dependency pattern-matching router
│   ├── Controllers/
│   │   └── PortalController.php
│   └── Views/
│       ├── layout.php      # Base template with responsive navigation
│       ├── dashboard.php   # Overview with metrics, recent runs & connections
│       ├── runs/           # Integration run listings and audit detail views
│       └── connections/    # Connection profiles and live probe testing
├── Dockerfile              # Container image definition with PHP 8.3 CLI server
├── compose.yaml            # Standalone Docker Compose definition
└── composer.json           # PSR-4 Autoloading definition
```

---

## 🚀 Getting Started

### 1. Running Locally (Directly with PHP CLI)

If you have PHP 8.2+ installed locally:

```bash
# Navigate to the portal directory
cd web/relayworks-portal-php

# Start the built-in development server pointing to public/
php -S localhost:8080 -t public
```

Open your browser at `http://localhost:8080`.

### 2. Running with Docker Compose

```bash
cd web/relayworks-portal-php
docker compose up -d
```

Access the portal at `http://localhost:8080`.

---

## ⚙️ Environment Variables

The portal connects directly to the RelayWorks Control Plane API. Configure the following environment variables if needed:

| Variable | Default | Description |
|---|---|---|
| `RELAYWORKS_API_URL` | `http://localhost:5080` | URL of the RelayWorks Control Plane API |
| `RELAYWORKS_TENANT_ID` | `tenant-default` | Active tenant ID header (`X-Tenant-Id`) |
| `RELAYWORKS_ACTOR_ID` | `portal-operator` | Operator identity header (`X-Actor-Id`) |
| `RELAYWORKS_AUTH_TOKEN` | *empty* | Optional JWT Bearer token if auth is enabled |
| `APP_ENV` | `development` | Environment name (development/production) |
