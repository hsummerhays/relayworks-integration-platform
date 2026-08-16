<?php

declare(strict_types=1);

namespace RelayWorks\Portal;

final readonly class Config
{
    public function __construct(
        public string $apiBaseUrl,
        public string $tenantId,
        public string $actorId,
        public string $appEnv,
        public string $authToken,
    ) {}

    public static function load(): self
    {
        return new self(
            apiBaseUrl: rtrim($_ENV['RELAYWORKS_API_URL'] ?? getenv('RELAYWORKS_API_URL') ?: 'http://localhost:5080', '/'),
            tenantId: $_ENV['RELAYWORKS_TENANT_ID'] ?? getenv('RELAYWORKS_TENANT_ID') ?: 'tenant-default',
            actorId: $_ENV['RELAYWORKS_ACTOR_ID'] ?? getenv('RELAYWORKS_ACTOR_ID') ?: 'portal-operator',
            appEnv: $_ENV['APP_ENV'] ?? getenv('APP_ENV') ?: 'development',
            authToken: $_ENV['RELAYWORKS_AUTH_TOKEN'] ?? getenv('RELAYWORKS_AUTH_TOKEN') ?: '',
        );
    }
}
