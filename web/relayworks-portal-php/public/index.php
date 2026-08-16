<?php

declare(strict_types=1);

// Lightweight PSR-4 compatible autoloader for standalone execution without composer install
spl_autoload_register(static function (string $class): void {
    $prefix = 'RelayWorks\\Portal\\';
    $baseDir = __DIR__ . '/../src/';

    $len = strlen($prefix);
    if (strncmp($prefix, $class, $len) !== 0) {
        return;
    }

    $relativeClass = substr($class, $len);
    $file = $baseDir . str_replace('\\', '/', $relativeClass) . '.php';

    if (file_exists($file)) {
        require $file;
    }
});

use RelayWorks\Portal\ApiClient;
use RelayWorks\Portal\Config;
use RelayWorks\Portal\Controllers\PortalController;
use RelayWorks\Portal\Router;

$config = Config::load();
$apiClient = new ApiClient($config);
$controller = new PortalController($apiClient, $config);

$router = new Router();

// Route definitions
$router->get('/', [$controller, 'dashboard']);
$router->get('/runs', [$controller, 'listRuns']);
$router->get('/runs/{id}', [$controller, 'showRun']);
$router->get('/connections', [$controller, 'listConnections']);
$router->get('/connections/{id}/test', [$controller, 'testConnection']);
$router->post('/connections/{id}/test', [$controller, 'testConnection']);

// Dispatch incoming request
$router->dispatch($_SERVER['REQUEST_METHOD'] ?? 'GET', $_SERVER['REQUEST_URI'] ?? '/');
