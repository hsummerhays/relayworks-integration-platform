<?php
/** @var list<array<string, mixed>> $runs */
/** @var string|null $nextCursor */
/** @var string|null $status */
/** @var string|null $connectionId */
?>

<div class="panel">
    <div class="panel-header">
        <h2 class="panel-title">Integration Runs</h2>
        <form method="GET" action="/runs" class="filter-form">
            <select name="status" class="form-select" onchange="this.form.submit()">
                <option value="">All Statuses</option>
                <option value="Pending" <?= $status === 'Pending' ? 'selected' : '' ?>>Pending</option>
                <option value="Running" <?= $status === 'Running' ? 'selected' : '' ?>>Running</option>
                <option value="Completed" <?= $status === 'Completed' ? 'selected' : '' ?>>Completed</option>
                <option value="Failed" <?= $status === 'Failed' ? 'selected' : '' ?>>Failed</option>
            </select>
        </form>
    </div>

    <div class="panel-body">
        <?php if (empty($runs)): ?>
            <p class="empty-state">No integration runs found matching criteria.</p>
        <?php else: ?>
            <div class="table-responsive">
                <table class="table">
                    <thead>
                        <tr>
                            <th>Run ID</th>
                            <th>Connection ID</th>
                            <th>Operation</th>
                            <th>Status</th>
                            <th>Progress (OK / Fail / Total)</th>
                            <th>Timestamp</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php foreach ($runs as $run): ?>
                            <tr>
                                <td>
                                    <a href="/runs/<?= urlencode($run['id'] ?? '') ?>" class="code-link">
                                        <?= htmlspecialchars($run['id'] ?? '') ?>
                                    </a>
                                </td>
                                <td class="text-muted font-mono"><?= htmlspecialchars(substr($run['connectionId'] ?? '', 0, 8)) ?>...</td>
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
                                    <?= (int)($run['successfulRecords'] ?? 0) ?> / <?= (int)($run['failedRecords'] ?? 0) ?> / <?= (int)($run['totalRecords'] ?? 0) ?>
                                </td>
                                <td class="text-muted"><?= htmlspecialchars($run['createdAtUtc'] ?? 'N/A') ?></td>
                                <td>
                                    <a href="/runs/<?= urlencode($run['id'] ?? '') ?>" class="btn btn-secondary btn-xs">View Records</a>
                                </td>
                            </tr>
                        <?php endforeach; ?>
                    </tbody>
                </table>
            </div>

            <?php if (!empty($nextCursor)): ?>
                <div class="pagination-footer">
                    <a href="/runs?cursor=<?= urlencode($nextCursor) ?>&status=<?= urlencode($status ?? '') ?>" class="btn btn-primary btn-sm">Next Page &rarr;</a>
                </div>
            <?php endif; ?>
        <?php endif; ?>
    </div>
</div>
