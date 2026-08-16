<?php
/** @var string $runId */
/** @var list<array<string, mixed>> $records */
/** @var string|null $nextCursor */
?>

<div class="page-actions-bar">
    <a href="/runs" class="btn btn-secondary btn-sm">&larr; Back to Runs</a>
    <div class="run-header-id">Run: <span class="font-mono"><?= htmlspecialchars($runId) ?></span></div>
</div>

<div class="panel">
    <div class="panel-header">
        <h2 class="panel-title">Integration Records</h2>
        <div class="view-filters">
            <a href="/runs/<?= urlencode($runId) ?>" class="tab-pill <?= !isset($_GET['view']) ? 'active' : '' ?>">All</a>
            <a href="/runs/<?= urlencode($runId) ?>?view=failed" class="tab-pill <?= ($_GET['view'] ?? '') === 'failed' ? 'active' : '' ?>">Failed Only</a>
        </div>
    </div>

    <div class="panel-body">
        <?php if (empty($records)): ?>
            <p class="empty-state">No record-level audit entries found for this run.</p>
        <?php else: ?>
            <div class="table-responsive">
                <table class="table">
                    <thead>
                        <tr>
                            <th>Record Key</th>
                            <th>Status</th>
                            <th>Payload / Error Detail</th>
                            <th>Processed At</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php foreach ($records as $rec): ?>
                            <tr>
                                <td class="font-mono"><strong><?= htmlspecialchars($rec['recordKey'] ?? 'N/A') ?></strong></td>
                                <td>
                                    <?php
                                        $st = strtolower($rec['status'] ?? 'pending');
                                        $badgeClass = match($st) {
                                            'success', 'completed' => 'badge-success',
                                            'failed', 'error' => 'badge-danger',
                                            default => 'badge-neutral',
                                        };
                                    ?>
                                    <span class="badge <?= $badgeClass ?>"><?= htmlspecialchars($rec['status'] ?? 'Unknown') ?></span>
                                </td>
                                <td>
                                    <?php if (!empty($rec['errorMessage'])): ?>
                                        <div class="text-danger small font-mono"><?= htmlspecialchars($rec['errorMessage']) ?></div>
                                    <?php endif; ?>
                                    <?php if (!empty($rec['payload'])): ?>
                                        <details class="payload-details">
                                            <summary>View Data</summary>
                                            <pre class="json-code"><?= htmlspecialchars(is_string($rec['payload']) ? $rec['payload'] : json_encode($rec['payload'], JSON_PRETTY_PRINT)) ?></pre>
                                        </details>
                                    <?php endif; ?>
                                </td>
                                <td class="text-muted"><?= htmlspecialchars($rec['processedAtUtc'] ?? $rec['createdAtUtc'] ?? 'N/A') ?></td>
                            </tr>
                        <?php endforeach; ?>
                    </tbody>
                </table>
            </div>

            <?php if (!empty($nextCursor)): ?>
                <div class="pagination-footer">
                    <a href="/runs/<?= urlencode($runId) ?>?cursor=<?= urlencode($nextCursor) ?>&view=<?= urlencode($_GET['view'] ?? '') ?>" class="btn btn-primary btn-sm">Load More Records &rarr;</a>
                </div>
            <?php endif; ?>
        <?php endif; ?>
    </div>
</div>
