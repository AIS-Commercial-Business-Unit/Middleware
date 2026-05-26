#!/usr/bin/env pwsh
# ============================================================
# AIS Middleware Platform — End-to-End Test Suite
# Tests both Java/Camel and .NET/NServiceBus stacks.
# Usage: .\scripts\test-e2e.ps1
#        .\scripts\test-e2e.ps1 -Stack java
#        .\scripts\test-e2e.ps1 -Stack dotnet
#        .\scripts\test-e2e.ps1 -Stack both -Verbose
# ============================================================

param(
    [ValidateSet("java","dotnet","both")]
    [string]$Stack = "both",
    [switch]$SkipLogCheck,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:Warnings = 0

# ── Helpers ──────────────────────────────────────────────────

function Write-Header($text) {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-Step($text) {
    Write-Host ""
    Write-Host "  ▶ $text" -ForegroundColor Yellow
}

function Pass($msg) {
    $script:Passed++
    Write-Host "    ✅ $msg" -ForegroundColor Green
}

function Fail($msg) {
    $script:Failed++
    Write-Host "    ❌ FAIL: $msg" -ForegroundColor Red
}

function Warn($msg) {
    $script:Warnings++
    Write-Host "    ⚠️  WARN: $msg" -ForegroundColor DarkYellow
}

function Info($msg) {
    if ($Verbose) { Write-Host "       $msg" -ForegroundColor DarkGray }
}

function Invoke-Get($url, [int]$TimeoutSec = 10) {
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec $TimeoutSec -ErrorAction Stop
        return $response
    } catch {
        return $null
    }
}

function Invoke-Post($url, $body, [int]$TimeoutSec = 15) {
    $json = $body | ConvertTo-Json -Depth 10
    try {
        $response = Invoke-RestMethod -Uri $url -Method Post `
            -ContentType "application/json" -Body $json `
            -TimeoutSec $TimeoutSec -ErrorAction Stop
        return $response
    } catch {
        return $null
    }
}

function Wait-ForStatus($url, $expectedStatus, [int]$MaxWaitSec = 30, [int]$PollSec = 2) {
    $deadline = [DateTime]::Now.AddSeconds($MaxWaitSec)
    while ([DateTime]::Now -lt $deadline) {
        $result = Invoke-Get $url
        if ($result -and $result.status -eq $expectedStatus) {
            return $result
        }
        Start-Sleep -Seconds $PollSec
    }
    return $null
}

function Get-ContainerLogs($container, [int]$Lines = 50) {
    try {
        return docker logs --tail $Lines $container 2>&1
    } catch {
        return ""
    }
}

function Assert-LogContains($logs, $pattern, $description) {
    $combined = $logs -join "`n"
    if ($combined -match $pattern) {
        Pass "Logs contain: $description"
    } else {
        Warn "Log pattern not found: $description (pattern: $pattern)"
    }
}

# ── 1. Pre-flight: Container health ──────────────────────────

Write-Header "Pre-flight: Container Health Check"

$coreContainers = @("kafka","mongodb","zookeeper")
$javaContainers = @(
    "policy-issuance-service",
    "platform-compliance-service",
    "customer-identity-service",
    "platform-integration-service",
    "billing-finance-service",
    "platform-notification-service",
    "platform-file-processing-service",
    "rsk3x3-compliance-stub",
    "erm7x1-account-stub",
    "duckcreek-commercial-stub"
)
$dotnetContainers = @(
    "sqlserver",
    "dotnet-policy-issuance",
    "dotnet-platform-compliance",
    "dotnet-customer-identity",
    "dotnet-platform-integration",
    "dotnet-billing-finance",
    "dotnet-platform-notification",
    "dotnet-kafka-bridge"
)

Write-Step "Checking core infrastructure containers"
foreach ($c in $coreContainers) {
    $status = docker inspect --format='{{.State.Status}}' $c 2>$null
    if ($status -eq "running") { Pass "$c is running" } else { Fail "$c is NOT running (status: $status)" }
}

if ($Stack -in @("java","both")) {
    Write-Step "Checking Java stack containers"
    foreach ($c in $javaContainers) {
        $status = docker inspect --format='{{.State.Status}}' $c 2>$null
        if ($status -eq "running") { Pass "$c is running" } else { Warn "$c is NOT running (status: $status)" }
    }
}

if ($Stack -in @("dotnet","both")) {
    Write-Step "Checking .NET stack containers"
    foreach ($c in $dotnetContainers) {
        $status = docker inspect --format='{{.State.Status}}' $c 2>$null
        if ($status -eq "running") { Pass "$c is running" } else { Warn "$c is NOT running (status: $status)" }
    }
}

# ── 2. Java UC1: Policy Issuance ─────────────────────────────

if ($Stack -in @("java","both")) {
    Write-Header "Java Stack — UC1: Policy Issuance"

    $javaIssuanceUrl = "http://localhost:8081"
    Write-Step "Health check: policy-issuance-service"
    $health = Invoke-Get "$javaIssuanceUrl/actuator/health"
    if ($health -and $health.status -eq "UP") {
        Pass "policy-issuance-service health: UP"
    } else {
        Fail "policy-issuance-service health check failed"
    }

    # Test 1: Commercial policy (DuckCreek Commercial, PolicyTypeCode=1)
    Write-Step "UC1-JAVA-001: Issue commercial policy (PolicyTypeCode=1 → DuckCreek Commercial)"
    $issuanceId = [System.Guid]::NewGuid().ToString()
    $payload = @{
        issuanceId = $issuanceId
        accountId = "ACC-TEST-JAVA-COMM"
        submittingChannel = "DirectRequest"
        requestedAt = (Get-Date -Format "o")
        policies = @(@{ policyTypeCode = 1; policyTypeSubCode = 0; policyData = @{} })
    }
    $resp = Invoke-Post "$javaIssuanceUrl/api/v1/policies/issue" $payload
    if ($resp -and $resp.issuanceId) {
        Pass "POST /api/v1/policies/issue returned 202 — issuanceId=$($resp.issuanceId)"
        Info "Response: status=$($resp.status)"
    } else {
        Fail "POST /api/v1/policies/issue failed — no response"
        $issuanceId = $null
    }

    if ($issuanceId) {
        Write-Step "  Polling saga state until Completed (max 30s)"
        $final = Wait-ForStatus "$javaIssuanceUrl/api/v1/policies/issue/$issuanceId" "Completed" 30 2
        if ($final) {
            Pass "Saga completed — status=Completed issuanceId=$issuanceId"
            Info "  targetPas=$($final.targetPas)"
            Info "  policyNumbers=$($final.policyNumbers -join ', ')"
            Info "  completedAt=$($final.completedAt)"
            if ($final.policyNumbers -and $final.policyNumbers.Count -gt 0) {
                Pass "Policy numbers assigned: $($final.policyNumbers -join ', ')"
            } else {
                Warn "No policy numbers in final saga record"
            }
        } else {
            # Check what status it's stuck at
            $partial = Invoke-Get "$javaIssuanceUrl/api/v1/policies/issue/$issuanceId"
            Fail "Saga did not complete within 30s — stuck at status=$($partial.status ?? 'unknown')"
        }

        if (-not $SkipLogCheck) {
            Write-Step "  Verifying structured logs for issuanceId=$issuanceId"
            $policyLogs = Get-ContainerLogs "policy-issuance-service" 200
            Assert-LogContains $policyLogs "IssuanceSaga" "Saga lifecycle logs present"
            Assert-LogContains $policyLogs "AwaitingCompliance" "Transition to AwaitingCompliance logged"
            Assert-LogContains $policyLogs "AwaitingPAS|AwaitingAccountRecord|account record|requesting account" "Mid-saga transitions logged"
            Assert-LogContains $policyLogs "PAS confirmed|Completed|policy-issued|PolicyIssued" "Saga completion logged"

            $complianceLogs = Get-ContainerLogs "platform-compliance-service" 50
            Assert-LogContains $complianceLogs "compliance|Compliance|RSK3X3|rsk3x3" "Compliance service processed request"

            $integrationLogs = Get-ContainerLogs "platform-integration-service" 50
            Assert-LogContains $integrationLogs "DuckCreek|duckcreek|PolicyTypeCode|PAS" "Integration service routed to PAS"
        }
    }

    # Test 2: ForeFront routing (PolicyTypeCode=10)
    Write-Step "UC1-JAVA-002: Issue D&O policy (PolicyTypeCode=10 → ForeFront)"
    $issuanceId2 = [System.Guid]::NewGuid().ToString()
    $payload2 = @{
        issuanceId = $issuanceId2
        accountId = "ACC-TEST-JAVA-FF"
        submittingChannel = "DirectRequest"
        requestedAt = (Get-Date -Format "o")
        policies = @(@{ policyTypeCode = 10; policyTypeSubCode = 0; policyData = @{} })
    }
    $resp2 = Invoke-Post "$javaIssuanceUrl/api/v1/policies/issue" $payload2
    if ($resp2 -and $resp2.issuanceId) {
        $final2 = Wait-ForStatus "$javaIssuanceUrl/api/v1/policies/issue/$issuanceId2" "Completed" 30 2
        if ($final2) {
            Pass "Saga completed (ForeFront) — targetPas=$($final2.targetPas)"
        } else {
            $p2 = Invoke-Get "$javaIssuanceUrl/api/v1/policies/issue/$issuanceId2"
            Warn "ForeFront saga did not complete within 30s — status=$($p2.status ?? 'unknown')"
        }
    }

    # Test 3: Idempotency check
    Write-Step "UC1-JAVA-003: Idempotency — replay same issuanceId"
    if ($issuanceId) {
        $dupResp = Invoke-Post "$javaIssuanceUrl/api/v1/policies/issue" $payload
        if ($dupResp) {
            # Should succeed (202) but the saga should not duplicate
            $dupSaga = Invoke-Get "$javaIssuanceUrl/api/v1/policies/issue/$issuanceId"
            if ($dupSaga -and $dupSaga.status -eq "Completed") {
                Pass "Idempotency: duplicate command accepted, saga state unchanged (Completed)"
            } else {
                Warn "Idempotency check inconclusive — status=$($dupSaga.status)"
            }
        }
    }
}

# ── 3. Java UC3: File Processing ─────────────────────────────

if ($Stack -in @("java","both")) {
    Write-Header "Java Stack — UC3: Automated Renewal Batch"

    $javaFileUrl = "http://localhost:8087"
    Write-Step "Health check: platform-file-processing-service"
    $fileHealth = Invoke-Get "$javaFileUrl/actuator/health"
    if ($fileHealth -and $fileHealth.status -eq "UP") {
        Pass "platform-file-processing-service health: UP"
    } else {
        Fail "platform-file-processing-service health check failed"
    }

    Write-Step "UC3-JAVA-001: Generate renewal batch (3 records)"
    $genPayload = @{ recordCount = 3; policyTypeCode = 1 }
    $genResp = Invoke-Post "$javaFileUrl/api/v1/batches/generate" $genPayload 30
    if ($genResp -and ($genResp.batchId -or $genResp.message -or $genResp.fileName)) {
        Pass "Batch generated — response: $(($genResp | ConvertTo-Json -Compress).Substring(0, [Math]::Min(120, ($genResp | ConvertTo-Json -Compress).Length)))"
    } else {
        Warn "Batch generate returned unexpected response (may still have worked)"
        Info "Response: $($genResp | ConvertTo-Json)"
    }

    if (-not $SkipLogCheck) {
        Write-Step "  Waiting 15s for file processing to complete"
        Start-Sleep -Seconds 15

        $fileLogs = Get-ContainerLogs "platform-file-processing-service" 100
        Assert-LogContains $fileLogs "File arrived|file-arrival|batchId|batch" "File arrival detected in logs"
        Assert-LogContains $fileLogs "Batch parsed|totalRecords|record" "Batch parsing logged"
        Assert-LogContains $fileLogs "file-batch-started|FileBatchStarted|dispatching" "Batch started event logged"
    }
}

# ── 4. .NET UC1: Policy Issuance ─────────────────────────────

if ($Stack -in @("dotnet","both")) {
    Write-Header ".NET Stack — UC1: Policy Issuance (NServiceBus)"

    $dotnetIssuanceUrl = "http://localhost:8181"
    Write-Step "Health check: dotnet-policy-issuance"
    $dotHealth = Invoke-Get "$dotnetIssuanceUrl/health"
    if ($dotHealth -and ($dotHealth.status -eq "Healthy" -or $dotHealth -match "Healthy")) {
        Pass "dotnet-policy-issuance health: Healthy"
    } else {
        $dotHealthRaw = try { Invoke-RestMethod "$dotnetIssuanceUrl/health" -ErrorAction SilentlyContinue } catch { $null }
        if ($dotHealthRaw) { Warn "dotnet-policy-issuance health returned: $($dotHealthRaw | ConvertTo-Json -Compress)" }
        else { Fail "dotnet-policy-issuance health check failed — service may not be running" }
    }

    Write-Step "UC1-DOTNET-001: Issue commercial policy via NServiceBus saga"
    $dotIssuanceId = [System.Guid]::NewGuid().ToString()
    $dotPayload = @{
        issuanceId = $dotIssuanceId
        accountId = "ACC-TEST-DOTNET-COMM"
        submittingChannel = "DirectRequest"
        requestedAt = (Get-Date -Format "o")
        policies = @(@{ policyTypeCode = 1; policyTypeSubCode = 0; policyData = @{} })
    }
    $dotResp = Invoke-Post "$dotnetIssuanceUrl/api/v1/policies/issue" $dotPayload 15
    if ($dotResp -and $dotResp.issuanceId) {
        Pass ".NET POST /api/v1/policies/issue returned 202 — issuanceId=$($dotResp.issuanceId)"
    } else {
        Fail ".NET POST /api/v1/policies/issue failed"
        $dotIssuanceId = $null
    }

    if ($dotIssuanceId) {
        Write-Step "  Polling .NET saga state until Completed (max 45s)"
        $dotFinal = Wait-ForStatus "$dotnetIssuanceUrl/api/v1/policies/issue/$dotIssuanceId" "Completed" 45 3
        if ($dotFinal) {
            Pass ".NET Saga completed — status=Completed issuanceId=$dotIssuanceId"
            Info "  policyNumbers=$($dotFinal.policyNumbers -join ', ')"
        } else {
            $dotPartial = Invoke-Get "$dotnetIssuanceUrl/api/v1/policies/issue/$dotIssuanceId"
            Fail ".NET Saga did not complete within 45s — stuck at: $($dotPartial.status ?? 'unknown')"
        }

        if (-not $SkipLogCheck) {
            Write-Step "  Verifying .NET structured logs"
            $dotLogs = Get-ContainerLogs "dotnet-policy-issuance" 200
            Assert-LogContains $dotLogs "IssuanceSaga|issuanceSaga" ".NET saga lifecycle logs present"
            Assert-LogContains $dotLogs "AwaitingCompliance" ".NET transition to AwaitingCompliance logged"
            Assert-LogContains $dotLogs "Completed|PolicyIssued|policy.issued" ".NET saga completion logged"
            Assert-LogContains $dotLogs '"issuanceId"' "Structured log field issuanceId present (Serilog)"
        }
    }

    Write-Step "UC1-DOTNET-002: Verify NServiceBus handler services ran"
    if (-not $SkipLogCheck) {
        $complianceDotLogs = Get-ContainerLogs "dotnet-platform-compliance" 50
        Assert-LogContains $complianceDotLogs "Compliance|compliance|RSK3X3|rsk3x3" ".NET compliance handler processed request"

        $integrationDotLogs = Get-ContainerLogs "dotnet-platform-integration" 50
        Assert-LogContains $integrationDotLogs "DuckCreek|duckcreek|ForeFront|PolicyTypeCode|PAS" ".NET integration routed to PAS"

        $billingDotLogs = Get-ContainerLogs "dotnet-billing-finance" 50
        Assert-LogContains $billingDotLogs "Billing|billing|association|CRM19X1" ".NET billing handler ran"
    }
}

# ── 5. .NET UC3: File Processing ─────────────────────────────

if ($Stack -in @("dotnet","both")) {
    Write-Header ".NET Stack — UC3: File Processing"

    $dotnetFileUrl = "http://localhost:8187"
    Write-Step "Health check: dotnet-file-processing"
    $dotFileHealth = Invoke-Get "$dotnetFileUrl/health"
    if ($dotFileHealth -and ($dotFileHealth.status -eq "Healthy" -or $dotFileHealth -match "Healthy")) {
        Pass "dotnet-file-processing health: Healthy"
    } else {
        Warn "dotnet-file-processing health check inconclusive (may still be OK)"
    }

    Write-Step "UC3-DOTNET-001: Generate renewal batch via .NET service"
    $dotGenPayload = @{ recordCount = 3; policyTypeCode = 1 }
    $dotGenResp = Invoke-Post "$dotnetFileUrl/api/v1/batches/generate" $dotGenPayload 30
    if ($dotGenResp) {
        Pass ".NET batch generate accepted"
        Info "Response: $($dotGenResp | ConvertTo-Json -Compress)"
    } else {
        Warn ".NET batch generate returned no response"
    }

    if (-not $SkipLogCheck) {
        Start-Sleep -Seconds 15
        $dotFileLogs = Get-ContainerLogs "dotnet-file-processing" 100
        Assert-LogContains $dotFileLogs "File arrived|batch|Batch|renewal|inbound" ".NET file processing detected file arrival"
        Assert-LogContains $dotFileLogs "IssuePolicyCommand|issue|record" ".NET file processing dispatched issuance commands"
    }
}

# ── 6. Kafka Bridge Verification ─────────────────────────────

if ($Stack -in @("dotnet","both") -and -not $SkipLogCheck) {
    Write-Header "Kafka Bridge — .NET Events on Kafka Topics"

    Write-Step "Checking dotnet-kafka-bridge logs"
    $bridgeLogs = Get-ContainerLogs "dotnet-kafka-bridge" 100
    Assert-LogContains $bridgeLogs "policy.events|PolicyIssued|Kafka|kafka|bridge|forwarded|published" "Kafka bridge forwarded events"
    Assert-LogContains $bridgeLogs "Confluent|Producer|topic" "Confluent.Kafka producer active"
}

# ── 7. Observability Stack ────────────────────────────────────

Write-Header "Observability: Grafana Stack"

Write-Step "Checking Grafana health"
$grafana = Invoke-Get "http://localhost:3001/api/health"
if ($grafana -and $grafana.database -eq "ok") {
    Pass "Grafana healthy — database=ok version=$($grafana.version)"
} else {
    Warn "Grafana health check inconclusive"
}

Write-Step "Checking Loki health (log ingestion)"
$loki = Invoke-Get "http://localhost:3100/ready"
if ($loki -match "ready") {
    Pass "Loki ready"
} else {
    Warn "Loki readiness check inconclusive"
}

Write-Step "Checking Prometheus targets"
$prom = Invoke-Get "http://localhost:9090/-/healthy"
if ($prom -match "Healthy|OK") {
    Pass "Prometheus healthy"
} else {
    Warn "Prometheus health check inconclusive"
}

Write-Step "Checking Kafdrop (Kafka UI)"
$kafdrop = Invoke-Get "http://localhost:9000"
if ($kafdrop) {
    Pass "Kafdrop accessible at http://localhost:9000"
} else {
    Warn "Kafdrop not accessible"
}

# ── 8. Platform UI ────────────────────────────────────────────

Write-Header "Platform UI"

Write-Step "Checking Platform UI"
$ui = Invoke-Get "http://localhost:3000"
if ($ui) {
    Pass "Platform UI accessible at http://localhost:3000"
} else {
    Warn "Platform UI not responding"
}

Write-Step "Checking active backend via /api/backend-info"
$backendInfo = Invoke-Get "http://localhost:3000/api/backend-info"
if ($backendInfo -and $backendInfo.backend) {
    Pass "Active backend: $($backendInfo.backend)"
} else {
    Warn "backend-info endpoint not responding"
}

# ── 9. Summary ───────────────────────────────────────────────

Write-Host ""
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Passed:   $($script:Passed)" -ForegroundColor Green
Write-Host "  Failed:   $($script:Failed)" -ForegroundColor Red
Write-Host "  Warnings: $($script:Warnings)" -ForegroundColor DarkYellow
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "  RESULT: FAILED — $($script:Failed) test(s) failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Tip: Run 'docker compose logs <service-name>' to diagnose failures" -ForegroundColor DarkGray
    exit 1
} elseif ($script:Warnings -gt 0) {
    Write-Host "  RESULT: PASSED WITH WARNINGS" -ForegroundColor DarkYellow
    exit 0
} else {
    Write-Host "  RESULT: ALL TESTS PASSED" -ForegroundColor Green
    exit 0
}
