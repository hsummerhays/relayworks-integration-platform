<?php
/** @var list<array<string, mixed>> $runs */
/** @var list<array<string, mixed>> $connections */
/** @var \RelayWorks\Portal\Config $config */
?>

<div class="metrics-grid">
    <div class="metric-card">
        <div class="metric-header">
            <span class="metric-title">Active Connections</span>
            <span class="metric-icon">🔌</span>
        </div>
        <div class="metric-value"><?= count($connections) ?></div>
        <div class="metric-footer">Configured profiles for tenant</div>
    </div>

    <div class="metric-card">
        <div class="metric-header">
            <span class="metric-title">Recent Runs</span>
            <span class="metric-icon">⚡</span>
        </div>
        <div class="metric-value"><?= count($runs) ?></div>
        <div class="metric-footer">Latest integration jobs</div>
    </div>

    <div class="metric-card">
        <div class="metric-header">
            <span class="metric-title">Environment</span>
            <span class="metric-icon">🌐</span>
        </div>
        <div class="metric-value capitalize"><?= htmlspecialchars($config->appEnv) ?></div>
        <div class="metric-footer"><?= htmlspecialchars($config->tenantId) ?></div>
    </div>
</div>

<div class="dashboard-sections">
    <div class="panel">
        <div class="panel-header">
            <h2 class="panel-title">Latest Integration Runs</h2>
            <a href="/runs" class="btn btn-secondary btn-sm">View All Runs &rarr;</a>
        </div>
        <div class="panel-body">
            <?php if (empty($runs)): ?>
                <p class="empty-state">No integration runs found for this tenant.</p>
            <?php else: ?>
                <div class="table-responsive">
                    <table class="table">
                        <thead>
                            <tr>
                                <th>Run ID</th>
                                <th>Operation</th>
                                <th>Status</th>
                                <th>Records (OK / Err)</th>
                                <th>Created</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <?php foreach ($runs as $run): ?>
                                <tr>
                                    <td>
                                        <a href="/runs/<?= urlencode($run['id'] ?? '') ?>" class="code-link">
                                            <?= htmlspecialchars(substr($run['id'] ?? '', 0, 8)) ?>...
                                        </a>
                                    </td>
                                    <td><strong><?= htmlspecialchars($run['operation'] ?? 'N/A') ?></strong></td>
                                    <td>
                                        <?php
                                            $st = strtolower($run['status'] ?? 'pending');
                                            $badgeClass = match($st) {
                                                'completed', 'succeeded' => 'badge-success',
                                                'failed' => 'badge-danger',
                                                'running', 'inprogress' => 'badge-warning',
                                                default => 'badge-neutral',
                                            };
                                        ?>
                                        <span class="badge <?= $badgeClass ?>"><?= htmlspecialchars($run['status'] ?? 'Unknown') ?></span>
                                    </td>
                                    <td>
                                        <?= (int)($run['successfulRecords'] ?? 0) ?> / <?= (int)($run['failedRecords'] ?? 0) ?>
                                        <span class="text-muted">(of <?= (int)($run['totalRecords'] ?? 0) ?>)</span>
                                    </td>
                                    <td class="text-muted"><?= htmlspecialchars($run['createdAtUtc'] ?? 'N/A') ?></td>
                                    <td>
                                        <a href="/runs/<?= urlencode($run['id'] ?? '') ?>" class="btn btn-ghost btn-xs">Details</a>
                                    </td>
                                </tr>
                            <?php endforeach; ?>
                        </tbody>
                    </table>
                </div>
            <?php endif; ?>
        </div>
    </div>

    <div class="panel">
        <div class="panel-header">
            <h2 class="panel-title">Active Connections</h2>
            <a href="/connections" class="btn btn-secondary btn-sm">Manage &rarr;</a>
        </div>
        <div class="panel-body">
            <?php if (empty($connections)): ?>
                <p class="empty-state">No active connection profiles available.</p>
            <?php else: ?>
                <div class="connection-cards-compact">
                    <?php foreach ($connections as $c): ?>
                        <div class="connection-item">
                            <div class="connection-icon"><?= strtoupper(substr($c['provider'] ?? 'P', 0, 2)) ?></div>
                            <div class="connection-info">
                                <span class="connection-name"><?= htmlspecialchars($c['name'] ?? 'Unnamed') ?></span>
                                <span class="connection-provider"><?= htmlspecialchars($c['provider'] ?? '') ?> &bull; <?= htmlspecialchars($c['authType'] ?? '') ?></span>
                            </div>
                            <a href="/connections/<?= urlencode($c['id'] ?? '') ?>/test" class="btn btn-outline btn-xs">Test</a>
                        </div>
                    <?php endforeach; ?>
                </div>
            <?php endif; ?>
        </div>
    </div>
</div>
