<?php

declare(strict_types=1);

namespace RelayWorks\Portal;

use RuntimeException;

final class ApiClient
{
    public function __construct(
        private readonly Config $config
    ) {}

    /**
     * @param array<string, scalar|null> $params
     * @return array<string, mixed>|list<mixed>
     */
    public function get(string $endpoint, array $params = []): array
    {
        $url = $this->config->apiBaseUrl . '/' . ltrim($endpoint, '/');
        if (!empty($params)) {
            $filteredParams = array_filter($params, static fn($val) => $val !== null && $val !== '');
            if (!empty($filteredParams)) {
                $url .= '?' . http_build_query($filteredParams);
            }
        }

        return $this->request('GET', $url);
    }

    /**
     * @param array<string, mixed> $data
     * @return array<string, mixed>
     */
    public function post(string $endpoint, array $data = []): array
    {
        $url = $this->config->apiBaseUrl . '/' . ltrim($endpoint, '/');
        return $this->request('POST', $url, $data);
    }

    /**
     * @param array<string, mixed>|null $body
     * @return array<string, mixed>|list<mixed>
     */
    private function request(string $method, string $url, ?array $body = null): array
    {
        $ch = curl_init();

        $headers = [
            'Accept: application/json',
            'X-Tenant-Id: ' . $this->config->tenantId,
            'X-Actor-Id: ' . $this->config->actorId,
        ];

        if ($this->config->authToken !== '') {
            $headers[] = 'Authorization: Bearer ' . $this->config->authToken;
        }

        $options = [
            CURLOPT_URL => $url,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CUSTOMREQUEST => $method,
            CURLOPT_TIMEOUT => 15,
            CURLOPT_CONNECTTIMEOUT => 5,
        ];

        if ($body !== null) {
            $jsonPayload = json_encode($body, JSON_THROW_ON_ERROR);
            $headers[] = 'Content-Type: application/json';
            $options[CURLOPT_POSTFIELDS] = $jsonPayload;
        }

        $options[CURLOPT_HTTPHEADER] = $headers;
        curl_setopt_array($ch, $options);

        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $error = curl_error($ch);
        curl_close($ch);

        if ($response === false) {
            throw new RuntimeException("API connection error to [{$url}]: {$error}");
        }

        if ($httpCode === 404) {
            return [];
        }

        $decoded = json_decode((string)$response, true);
        if ($httpCode >= 400) {
            $msg = is_array($decoded) && isset($decoded['title']) ? (string)$decoded['title'] : "HTTP {$httpCode}";
            if (isset($decoded['detail'])) {
                $msg .= ": " . $decoded['detail'];
            }
            throw new RuntimeException("Control Plane API error [{$httpCode}]: {$msg}");
        }

        return is_array($decoded) ? $decoded : [];
    }
}
