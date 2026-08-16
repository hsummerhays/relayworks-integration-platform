<?php
/** @var string $connectionId */
/** @var array<string, mixed>|null $testResult */
?>

<div class="page-actions-bar">
    <a href="/connections" class="btn btn-secondary btn-sm">&larr; Back to Connections</a>
</div>

<div class="panel">
    <div class="panel-header">
        <h2 class="panel-title">Connection Test Diagnostic</h2>
        <form method="POST" action="/connections/<?= urlencode($connectionId) ?>/test">
            <button type="submit" class="btn btn-primary btn-sm">▶ Run New Test</button>
        </form>
    </div>

    <div class="panel-body">
        <div class="diag-meta">
            <span class="prop-label">Target Connection ID:</span>
            <span class="font-mono"><?= htmlspecialchars($connectionId) ?></span>
        </div>

        <?php if (empty($testResult)): ?>
            <div class="empty-state">
                <p>No tests have been executed for this connection yet.</p>
                <form method="POST" action="/connections/<?= urlencode($connectionId) ?>/test" style="margin-top: 1rem;">
                    <button type="submit" class="btn btn-primary">Dispatch Connection Probe</button>
                </form>
            </div>
        <?php else: ?>
            <div class="test-result-box">
                <div class="result-header">
                    <h3>Latest Test Result</h3>
                    <?php
                        $status = strtolower($testResult['status'] ?? 'pending');
                        $badgeClass = match($status) {
                            'succeeded', 'passed', 'completed' => 'badge-success',
                            'failed' => 'badge-danger',
                            default => 'badge-warning'
                        };
                    ?>
                    <span class="badge <?= $badgeClass ?>"><?= htmlspecialchars($testResult['status'] ?? 'Pending') ?></span>
                </div>

                <div class="result-details">
                    <pre class="json-code"><?= htmlspecialchars(json_encode($testResult, JSON_PRETTY_PRINT)) ?></pre>
                </div>
            </div>
        <?php endif; ?>
    </div>
</div>
