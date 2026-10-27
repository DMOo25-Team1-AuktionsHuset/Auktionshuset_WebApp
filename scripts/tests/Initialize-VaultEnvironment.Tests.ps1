#Requires -Version 5.1
# Offline contract tests. HTTP, prompts, output and environment writes are
# replaced below; no real Vault request or Windows user environment write occurs.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\Initialize-VaultEnvironment.ps1')

function Assert-SetupTest {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Reset-SetupTest {
    $script:requests = [System.Collections.Generic.List[object]]::new()
    $script:messages = [System.Collections.Generic.List[string]]::new()
    $script:savedValues = $null
    $script:creationPath = 'auth/approle/role/auction-backend/secret-id'
    $script:requestFails = $false
    $script:returnSecret = $true
    $script:saveFails = $false
}

function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if (-not $AsSecureString) { throw 'Unexpected unprotected prompt in test.' }
    return ConvertTo-SecureString 'offline-wrapping-token' -AsPlainText -Force
}

function Write-Host {
    param([object]$Object)
    $script:messages.Add([string]$Object)
}

function Save-VaultSetupEnvironment {
    param([hashtable]$Values)
    if ($script:saveFails) { throw 'offline simulated registry error' }
    $script:savedValues = $Values.Clone()
}

function Invoke-RestMethod {
    [CmdletBinding()]
    param(
        [Uri]$Uri,
        [string]$Method,
        [hashtable]$Headers,
        [string]$Body,
        [string]$ContentType,
        [int]$MaximumRedirection,
        [int]$TimeoutSec,
        [switch]$UseBasicParsing
    )

    Assert-SetupTest ($Method -eq 'Post') 'Requests must use POST.'
    Assert-SetupTest ($Uri.Scheme -eq 'https') 'Requests must use HTTPS.'
    Assert-SetupTest ($MaximumRedirection -eq 0) 'Credential requests must not follow redirects.'
    Assert-SetupTest ($TimeoutSec -eq 20) 'Requests must have a bounded timeout.'
    Assert-SetupTest ($Headers['X-Vault-Token'] -eq 'offline-wrapping-token') 'Wrong request token.'
    $script:requests.Add([pscustomobject]@{ Path = $Uri.AbsolutePath; Body = $Body })
    if ($script:requestFails) { throw 'offline simulated expired token' }

    switch ($Uri.AbsolutePath) {
        '/v1/sys/wrapping/lookup' {
            return [pscustomobject]@{ data = [pscustomobject]@{ creation_path = $script:creationPath } }
        }
        '/v1/sys/wrapping/unwrap' {
            if ($script:returnSecret) {
                return [pscustomobject]@{ data = [pscustomobject]@{ secret_id = 'offline-secret-id' } }
            }
            return [pscustomobject]@{ data = [pscustomobject]@{} }
        }
        default { throw 'Unexpected endpoint in test.' }
    }
}

function Assert-SetupFailure {
    param([scriptblock]$Action, [string]$ExpectedMessage)
    $caught = $null
    try { & $Action } catch { $caught = $_ }
    Assert-SetupTest ($null -ne $caught) 'Expected a terminating failure.'
    Assert-SetupTest ($caught.Exception.Message -like ('*' + $ExpectedMessage + '*')) 'Unexpected failure message.'
    Assert-SetupTest (-not $caught.Exception.Message.Contains('offline-wrapping-token')) 'Token leaked in error.'
    Assert-SetupTest (-not $caught.Exception.Message.Contains('offline-secret-id')) 'SecretID leaked in error.'
}

$testCases = [ordered]@{
    'Successful setup saves three values, unwraps once and does not print credentials' = {
        $successOutput = @(Initialize-VaultEnvironment -VaultAddress 'https://vault.example.test:8200/' -RoleId ' offline-role-id ')
        Assert-SetupTest ($script:requests.Count -eq 2) 'Expected lookup then unwrap.'
        Assert-SetupTest ($script:requests[0].Path -eq '/v1/sys/wrapping/lookup') 'Lookup must precede unwrap.'
        Assert-SetupTest ($script:requests[1].Path -eq '/v1/sys/wrapping/unwrap') 'Second request must unwrap.'
        Assert-SetupTest ($script:requests[1].Body -eq '{}') 'Unwrap token must not also be passed in the body.'
        Assert-SetupTest ($script:savedValues.Count -eq 3) 'Expected three saved variables.'
        Assert-SetupTest ($script:savedValues.VAULT_ADDR -eq 'https://vault.example.test:8200') 'Address normalization failed.'
        Assert-SetupTest ($script:savedValues.VAULT_ROLE_ID -eq 'offline-role-id') 'RoleID normalization failed.'
        Assert-SetupTest ($script:savedValues.VAULT_SECRET_ID -eq 'offline-secret-id') 'Wrong SecretID saved.'
        Assert-SetupTest ($successOutput.Count -eq 0) 'Setup must not emit credential-bearing success output.'
        $printed = $script:messages -join ' '
        Assert-SetupTest (-not $printed.Contains('offline-wrapping-token')) 'Wrapping token printed.'
        Assert-SetupTest (-not $printed.Contains('offline-secret-id')) 'SecretID printed.'
    }
    'A token from another path is not unwrapped or saved' = {
        $script:creationPath = 'sys/wrapping/wrap'
        Assert-SetupFailure { Initialize-VaultEnvironment -VaultAddress 'https://vault.example.test' -RoleId 'offline-role-id' } 'ikke oprettet til auction-backend'
        Assert-SetupTest ($script:requests.Count -eq 1) 'Foreign token must not be unwrapped.'
        Assert-SetupTest ($null -eq $script:savedValues) 'Foreign token must not alter configuration.'
    }
    'Expired token and network failures do not save configuration' = {
        $script:requestFails = $true
        Assert-SetupFailure { Initialize-VaultEnvironment -VaultAddress 'https://vault.example.test' -RoleId 'offline-role-id' } 'Vault-kaldet mislykkedes'
        Assert-SetupTest ($script:requests.Count -eq 1) 'Failed lookup must not be retried or unwrapped.'
        Assert-SetupTest ($null -eq $script:savedValues) 'Failed lookup must not save configuration.'
    }
    'Unwrapped data without a SecretID is rejected' = {
        $script:returnSecret = $false
        Assert-SetupFailure { Initialize-VaultEnvironment -VaultAddress 'https://vault.example.test' -RoleId 'offline-role-id' } 'ikke et SecretID'
        Assert-SetupTest ($null -eq $script:savedValues) 'Malformed response must not save configuration.'
    }
    'HTTP and UI paths are rejected before sending a credential' = {
        foreach ($invalidAddress in @('http://vault.example.test', 'https://vault.example.test/ui/', 'https://user:password@vault.example.test', 'https://vault.example.test/?token=x', 'https://vault.example.test/#fragment')) {
            Assert-SetupFailure { Initialize-VaultEnvironment -VaultAddress $invalidAddress -RoleId 'offline-role-id' } 'HTTPS-adresse'
        }
        Assert-SetupTest ($script:requests.Count -eq 0) 'Invalid address must not receive a token.'
        Assert-SetupTest ($null -eq $script:savedValues) 'Invalid address must not save configuration.'
    }
    'A local save failure explains that the one-time token has been consumed' = {
        $script:saveFails = $true
        Assert-SetupFailure { Initialize-VaultEnvironment -VaultAddress 'https://vault.example.test' -RoleId 'offline-role-id' } 'engangstokenet er nu brugt'
        Assert-SetupTest ($script:requests.Count -eq 2) 'A save error must not trigger another unwrap.'
    }
}

$passed = 0
foreach ($testCase in $testCases.GetEnumerator()) {
    Reset-SetupTest
    $tlsBeforeTest = [Net.ServicePointManager]::SecurityProtocol
    & $testCase.Value
    Assert-SetupTest ([Net.ServicePointManager]::SecurityProtocol -eq $tlsBeforeTest) 'TLS settings must be restored after success or failure.'
    $passed++
    Microsoft.PowerShell.Utility\Write-Host ('PASS: ' + $testCase.Key)
}
Microsoft.PowerShell.Utility\Write-Host ("$passed offline tests passed. No network requests or user environment writes were performed.")
