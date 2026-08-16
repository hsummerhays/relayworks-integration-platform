<?php

declare(strict_types=1);

namespace RelayWorks\Portal\Tests;

use Throwable;

final class TestRunner
{
    private int $passed = 0;
    private int $failed = 0;
    /** @var list<string> */
    private array $errors = [];

    public function test(string $name, callable $fn): void
    {
        try {
            $fn();
            $this->passed++;
            echo " \033[32m✔\033[0m {$name}\n";
        } catch (Throwable $e) {
            $this->failed++;
            $errorMsg = " \033[31m✖\033[0m {$name}\n    \033[31m" . $e->getMessage() . "\033[0m in " . $e->getFile() . ":" . $e->getLine();
            $this->errors[] = $errorMsg;
            echo $errorMsg . "\n";
        }
    }

    public function assertEquals(mixed $expected, mixed $actual, string $message = ''): void
    {
        if ($expected !== $actual) {
            $msg = $message ?: "Expected " . var_export($expected, true) . ", got " . var_export($actual, true);
            throw new \RuntimeException($msg);
        }
    }

    public function assertTrue(bool $condition, string $message = 'Expected true, got false'): void
    {
        if (!$condition) {
            throw new \RuntimeException($message);
        }
    }

    public function summary(): int
    {
        echo "\n--------------------------------------------\n";
        $total = $this->passed + $this->failed;
        echo "PHP Portal Tests: {$total} total | \033[32m{$this->passed} passed\033[0m | \033[31m{$this->failed} failed\033[0m\n";
        return $this->failed === 0 ? 0 : 1;
    }
}
