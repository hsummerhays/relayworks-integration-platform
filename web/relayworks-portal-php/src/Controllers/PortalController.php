<?php

declare(strict_types=1);

namespace RelayWorks\Portal\Controllers;

use RelayWorks\Portal\ApiClient;
use RelayWorks\Portal\Config;
use Throwable;

final class PortalController
{
    public function __construct(
        private readonly ApiClient $api,
        private readonly Config $config
    ) {}

    public function dashboard(): void
    {
        $error = null;
        $runs = [];
        $connections = [];

        try {
            /** @var array<string, mixed> $runsResponse */
            $runsResponse = $this->api->get('/api/integration-runs', ['pageSize' => 5]);
            $runs = $runsResponse['items'] ?? $runsResponse['runs'] ?? [];

            /** @var list<mixed> $connections */
            $connections = $this->api->get('/api/connections');
        } catch (Throwable $e) {
            $error = $e->getMessage();
        }

        $this->render('dashboard', [
            'title' => 'Dashboard Overview',
            'runs' => $runs,
            'connections' => $connections,
            'config' => $this->config,
            'error' => $error,
        ]);
    }

    public function listRuns(): void
    {
        $error = null;
        $runs = [];
        $status = $_GET['status'] ?? null;
        $connectionId = $_GET['connectionId'] ?? null;
        $cursor = $_GET['cursor'] ?? null;
        $nextCursor = null;

        try {
            $res = $this->api->get('/api/integration-runs', [
                'status' => $status,
                'connectionId' => $connectionId,
                'cursor' => $cursor,
                'pageSize' => 20,
            ]);
            $runs = $res['items'] ?? $res['runs'] ?? [];
            $nextCursor = $res['nextCursor'] ?? null;
        } catch (Throwable $e) {
            $error = $e->getMessage();
        }

        $this->render('runs/index', [
            'title' => 'Integration Runs',
            'runs' => $runs,
            'nextCursor' => $nextCursor,
            'status' => $status,
            'connectionId' => $connectionId,
            'config' => $this->config,
            'error' => $error,
        ]);
    }

    /**
     * @param array<string, string> $params
     */
    public function showRun(array $params): void
    {
        $runId = $params['id'] ?? '';
        $error = null;
        $records = [];
        $nextCursor = null;

        try {
            $recordsRes = $this->api->get("/api/integration-runs/{$runId}/records", [
                'pageSize' => 50,
                'cursor' => $_GET['cursor'] ?? null,
                'view' => $_GET['view'] ?? null,
            ]);
            $records = $recordsRes['items'] ?? $recordsRes['records'] ?? [];
            $nextCursor = $recordsRes['nextCursor'] ?? null;
        } catch (Throwable $e) {
            $error = $e->getMessage();
        }

        $this->render('runs/show', [
            'title' => "Run Details: {$runId}",
            'runId' => $runId,
            'records' => $records,
            'nextCursor' => $nextCursor,
            'config' => $this->config,
            'error' => $error,
        ]);
    }

    public function listConnections(): void
    {
        $error = null;
        $connections = [];

        try {
            $connections = $this->api->get('/api/connections');
        } catch (Throwable $e) {
            $error = $e->getMessage();
        }

        $this->render('connections/index', [
            'title' => 'Connection Profiles',
            'connections' => $connections,
            'config' => $this->config,
            'error' => $error,
        ]);
    }

    /**
     * @param array<string, string> $params
     */
    public function testConnection(array $params): void
    {
        $connectionId = $params['id'] ?? '';
        $error = null;
        $testResult = null;

        if ($_SERVER['REQUEST_METHOD'] === 'POST') {
            try {
                $testResult = $this->api->post("/api/connections/{$connectionId}/tests");
            } catch (Throwable $e) {
                $error = $e->getMessage();
            }
        } else {
            try {
                $testResult = $this->api->get("/api/connections/{$connectionId}/tests/latest");
            } catch (Throwable $e) {
                $error = $e->getMessage();
            }
        }

        $this->render('connections/test', [
            'title' => "Connection Test - {$connectionId}",
            'connectionId' => $connectionId,
            'testResult' => $testResult,
            'config' => $this->config,
            'error' => $error,
        ]);
    }

    /**
     * @param array<string, mixed> $data
     */
    private function render(string $view, array $data = []): void
    {
        extract($data);
        $viewFile = __DIR__ . "/../Views/{$view}.php";
        
        ob_start();
        if (file_exists($viewFile)) {
            require $viewFile;
        } else {
            echo "<p>View [{$view}] not found.</p>";
        }
        $content = ob_get_clean();

        require __DIR__ . '/../Views/layout.php';
    }
}
