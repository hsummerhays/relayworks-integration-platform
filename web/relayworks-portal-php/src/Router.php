<?php

declare(strict_types=1);

namespace RelayWorks\Portal;

final class Router
{
    /** @var array<string, array<string, callable>> */
    private array $routes = [];

    public function get(string $path, callable $handler): void
    {
        $this->routes['GET'][$path] = $handler;
    }

    public function post(string $path, callable $handler): void
    {
        $this->routes['POST'][$path] = $handler;
    }

    public function dispatch(string $method, string $uri): void
    {
        $parsedUri = parse_url($uri, PHP_URL_PATH) ?: '/';
        $path = rtrim($parsedUri, '/') ?: '/';

        $methodRoutes = $this->routes[strtoupper($method)] ?? [];

        // 1. Direct match
        if (isset($methodRoutes[$path])) {
            $methodRoutes[$path]();
            return;
        }

        // 2. Pattern match e.g. /runs/{id}
        foreach ($methodRoutes as $routePattern => $handler) {
            $regex = preg_replace('/\{([a-zA-Z0-9_]+)\}/', '(?P<$1>[^/]+)', $routePattern);
            $regex = '#^' . $regex . '$#';

            if (preg_match($regex, $path, $matches)) {
                $params = array_filter($matches, static fn($k) => !is_int($k), ARRAY_FILTER_USE_KEY);
                $handler($params);
                return;
            }
        }

        http_response_code(404);
        require __DIR__ . '/Views/404.php';
    }
}
