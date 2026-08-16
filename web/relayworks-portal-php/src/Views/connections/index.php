<?php
/** @var list<array<string, mixed>> $connections */
?>

<div class="panel">
    <div class="panel-header">
        <h2 class="panel-title">Active Connections</h2>
    </div>

    <div class="panel-body">
        <?php if (empty($connections)): ?>
            <p class="empty-state">No connection profiles configured for this tenant.</p>
        <?php else: ?>
            <div class="connections-grid">
                <?php foreach ($connections as $c): ?>
                    <div class="connection-card">
                        <div class="card-top">
                            <div class="connection-avatar"><?= strtoupper(substr($c['provider'] ?? 'P', 0, 2)) ?></div>
                            <div class="card-heading">
                                <h3><?= htmlspecialchars($c['name'] ?? 'Unnamed Profile') ?></h3>
                                <span class="provider-tag"><?= htmlspecialchars($c['provider'] ?? '') ?></span>
                            </div>
                        </div>

                        <div class="card-props">
                            <div class="prop-row">
                                <span class="prop-label">Auth Type:</span>
                                <span class="prop-val"><?= htmlspecialchars($c['authType'] ?? 'N/A') ?></span>
                            </div>
                            <div class="prop-row">
                                <span class="prop-label">Idempotency Key:</span>
                                <span class="prop-val"><?= !empty($c['supportsIdempotencyKey']) ? '✅ Supported' : '❌ Disabled' ?></span>
                            </div>
                            <div class="prop-row">
                                <span class="prop-label">Read After Write:</span>
                                <span class="prop-val"><?= !empty($c['supportsReadAfterWrite']) ? '✅ Enabled' : '❌ Disabled' ?></span>
                            </div>
                        </div>

                        <div class="card-actions">
                            <a href="/connections/<?= urlencode($c['id'] ?? '') ?>/test" class="btn btn-primary btn-sm btn-block">
                                Test Health & Connectivity
                            </a>
                        </div>
                    </div>
                <?php endforeach; ?>
            </div>
        <?php endif; ?>
    </div>
</div>
