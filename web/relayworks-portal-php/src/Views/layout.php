<?php
/** @var string $title */
/** @var string $content */
/** @var \RelayWorks\Portal\Config $config */
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars($title ?? 'RelayWorks Portal') ?> | RelayWorks</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="/css/style.css">
</head>
<body>
    <div class="app-layout">
        <!-- Sidebar -->
        <aside class="sidebar">
            <div class="brand">
                <div class="brand-icon">RW</div>
                <div class="brand-text">
                    <span class="brand-title">RelayWorks</span>
                    <span class="brand-subtitle">PHP Portal</span>
                </div>
            </div>

            <nav class="nav-menu">
                <a href="/" class="nav-item <?= $_SERVER['REQUEST_URI'] === '/' ? 'active' : '' ?>">
                    <svg class="nav-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/></svg>
                    <span>Overview</span>
                </a>
                <a href="/runs" class="nav-item <?= str_starts_with($_SERVER['REQUEST_URI'], '/runs') ? 'active' : '' ?>">
                    <svg class="nav-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                    <span>Integration Runs</span>
                </a>
                <a href="/connections" class="nav-item <?= str_starts_with($_SERVER['REQUEST_URI'], '/connections') ? 'active' : '' ?>">
                    <svg class="nav-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
                    <span>Connections</span>
                </a>
            </nav>

            <div class="tenant-badge">
                <span class="tenant-label">Tenant ID</span>
                <span class="tenant-val"><?= htmlspecialchars($config->tenantId) ?></span>
                <span class="api-endpoint"><?= htmlspecialchars($config->apiBaseUrl) ?></span>
            </div>
        </aside>

        <!-- Main Content Area -->
        <div class="main-wrapper">
            <header class="topbar">
                <h1 class="page-title"><?= htmlspecialchars($title ?? 'Dashboard') ?></h1>
                <div class="topbar-actions">
                    <span class="badge badge-success">API Connected</span>
                    <span class="actor-tag"><?= htmlspecialchars($config->actorId) ?></span>
                </div>
            </header>

            <main class="content-container">
                <?php if (!empty($error)): ?>
                    <div class="alert alert-danger">
                        <strong>API Error:</strong> <?= htmlspecialchars($error) ?>
                    </div>
                <?php endif; ?>

                <?= $content ?>
            </main>
        </div>
    </div>
    <script src="/js/portal.js"></script>
</body>
</html>
