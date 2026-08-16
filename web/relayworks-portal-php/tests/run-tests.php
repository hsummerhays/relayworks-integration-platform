<?php

declare(strict_types=1);

require_once __DIR__ . '/TestRunner.php';

// Autoloader
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
        require_once $file;
    }
});

use RelayWorks\Portal\Config;
use RelayWorks\Portal\Router;
use RelayWorks\Portal\Tests\TestRunner;

$t = new TestRunner();

echo "Running RelayWorks PHP Portal Test Suite...\n\n";

// --- Config Tests ---
$t->test('Config loads default environment fallback values', static function () use ($t): void {
    $cfg = new Config(
        apiBaseUrl: 'http://localhost:5000',
        tenantId: 'tenant-test',
        actorId: 'test-actor',
        appEnv: 'testing',
        authToken: 'token123'
    );

    $t->assertEquals('http://localhost:5000', $cfg->apiBaseUrl);
    $t->assertEquals('tenant-test', $cfg->tenantId);
    $t->assertEquals('test-actor', $cfg->actorId);
    $t->assertEquals('testing', $cfg->appEnv);
    $t->assertEquals('token123', $cfg->authToken);
});

// --- Router Tests ---
$t->test('Router matches direct exact GET routes', static function () use ($t): void {
    $router = new Router();
    $called = false;

    $router->get('/runs', static function () use (&$called): void {
        $called = true;
    });

    $router->dispatch('GET', '/runs');
    $t->assertTrue($called, 'Exact GET route should be invoked');
});

$t->test('Router trims trailing slashes from path', static function () use ($t): void {
    $router = new Router();
    $called = false;

    $router->get('/connections', static function () use (&$called): void {
        $called = true;
    });

    $router->dispatch('GET', '/connections/');
    $t->assertTrue($called, 'Trailing slash path should match route');
});

$t->test('Router captures dynamic route parameters', static function () use ($t): void {
    $router = new Router();
    $capturedId = null;

    $router->get('/runs/{id}', static function (array $params) use (&$capturedId): void {
        $capturedId = $params['id'] ?? null;
    });

    $router->dispatch('GET', '/runs/run-987abc');
    $t->assertEquals('run-987abc', $capturedId, 'Dynamic id parameter should match');
});

$t->test('Router handles multiple dynamic parameters', static function () use ($t): void {
    $router = new Router();
    $captured = [];

    $router->get('/connections/{connectionId}/tests/{testId}', static function (array $params) use (&$captured): void {
        $captured = $params;
    });

    $router->dispatch('GET', '/connections/conn-1/tests/test-99');
    $t->assertEquals('conn-1', $captured['connectionId'] ?? null);
    $t->assertEquals('test-99', $captured['testId'] ?? null);
});

$t->test('Router distinguishes HTTP methods', static function () use ($t): void {
    $router = new Router();
    $getMethodCalled = false;
    $postMethodCalled = false;

    $router->get('/test-endpoint', static function () use (&$getMethodCalled): void {
        $getMethodCalled = true;
    });

    $router->post('/test-endpoint', static function () use (&$postMethodCalled): void {
        $postMethodCalled = true;
    });

    $router->dispatch('POST', '/test-endpoint');
    $t->assertTrue($postMethodCalled, 'POST route handler should be invoked');
    $t->assertTrue(!$getMethodCalled, 'GET route handler should not be invoked on POST');
});

$t->test('Router strips query string before dispatching', static function () use ($t): void {
    $router = new Router();
    $called = false;

    $router->get('/runs', static function () use (&$called): void {
        $called = true;
    });

    $router->dispatch('GET', '/runs?status=Completed&pageSize=50');
    $t->assertTrue($called, 'Query parameters should not prevent route matching');
});

// Exit with status code
$exitCode = $t->summary();
exit($exitCode);
