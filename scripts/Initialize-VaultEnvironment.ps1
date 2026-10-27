#Requires -Version 5.1
<#
.SYNOPSIS
Sets up the current Windows user's Vault environment from a single-use wrapping token.
.DESCRIPTION
Run interactively. The wrapping token is read without echoing and is never accepted
as a command-line argument. HTTPS certificate validation remains enabled.
See docs/team-setup.md before running this script.
#>
[CmdletBinding()]
param(
    [string]$VaultAddress,
    [string]$RoleId
)

function Resolve-VaultAddress {
    param([string]$Address)

    $vaultUri = $null
    if (-not [Uri]::TryCreate($Address, [UriKind]::Absolute, [ref]$vaultUri) -or
        $vaultUri.Scheme -ne 'https' -or
        $vaultUri.UserInfo -or $vaultUri.Query -or $vaultUri.Fragment -or
        $vaultUri.AbsolutePath.Trim('/') -ne '') {
        throw 'Brug Vaults HTTPS-adresse med host og port, uden /ui/, loginoplysninger eller andre stier.'
    }

    return $vaultUri.GetLeftPart([UriPartial]::Authority)
}

function Invoke-VaultSetupRequest {
    param(
        [string]$Address,
        [string]$Path,
        [hashtable]$Headers,
        [string]$Body = '{}'
    )

    try {
        # Do not follow redirects with a credential or bypass certificate checks.
        return Invoke-RestMethod -Uri ($Address + '/v1/' + $Path) -Method Post `
            -Headers $Headers -Body $Body -ContentType 'application/json' `
            -MaximumRedirection 0 -TimeoutSec 20 -UseBasicParsing -ErrorAction Stop
    }
    catch {
        # Avoid printing Vault responses or request details containing credentials.
        throw 'Vault-kaldet mislykkedes. Kontrollér VPN, HTTPS-adresse og certifikat. Tokenet kan også være udløbet eller brugt. Bed den projektansvarlige om hjælp og eventuelt et nyt engangstoken.'
    }
}

function Get-VaultSetupSecret {
    param(
        [string]$Address,
        [System.Security.SecureString]$WrappingToken
    )

    $plainToken = $null
    $headers = @{}
    $lookupBody = $null
    $unwrapped = $null
    try {
        $plainToken = ([System.Net.NetworkCredential]::new('', $WrappingToken)).Password.Trim()
        if ([string]::IsNullOrWhiteSpace($plainToken) -or $plainToken -match '\s') {
            throw 'Indsæt kun værdien wrapping_token, uden feltnavn, mellemrum eller anførselstegn.'
        }

        $headers['X-Vault-Token'] = $plainToken
        $lookupBody = @{ token = $plainToken } | ConvertTo-Json -Compress
        $lookup = Invoke-VaultSetupRequest -Address $Address -Path 'sys/wrapping/lookup' `
            -Headers $headers -Body $lookupBody

        $expectedPath = 'auth/approle/role/auction-backend/secret-id'
        if ($lookup.data.creation_path -cne $expectedPath) {
            throw 'Engangstokenet er ikke oprettet til auction-backend. Intet er pakket ud eller gemt. Kontakt den projektansvarlige.'
        }

        # For unwrap, the wrapping token must appear only in the header, not also
        # in the body. Passing it twice can consume the token without returning data.
        $unwrapped = Invoke-VaultSetupRequest -Address $Address -Path 'sys/wrapping/unwrap' `
            -Headers $headers

        $secretId = [string]$unwrapped.data.secret_id
        if ([string]::IsNullOrWhiteSpace($secretId)) {
            throw 'Vault returnerede ikke et SecretID. Ingen miljøvariabler er ændret. Kontakt den projektansvarlige.'
        }

        return $secretId
    }
    finally {
        $headers.Clear()
        $plainToken = $null
        $lookupBody = $null
        $unwrapped = $null
    }
}

function Save-VaultSetupEnvironment {
    param([hashtable]$Values)

    # User-scoped environment variables match the application's existing setup.
    # Windows does not encrypt these values; this is development configuration.
    foreach ($name in @('VAULT_ADDR', 'VAULT_ROLE_ID', 'VAULT_SECRET_ID')) {
        [Environment]::SetEnvironmentVariable($name, $Values[$name], 'User')
        [Environment]::SetEnvironmentVariable($name, $Values[$name], 'Process')
    }
}

function Initialize-VaultEnvironment {
    [CmdletBinding()]
    param([string]$VaultAddress, [string]$RoleId)

    if ($env:OS -ne 'Windows_NT') {
        throw 'Dette script er beregnet til Windows. Følg vejledningen på din udviklings-pc.'
    }

    if ([string]::IsNullOrWhiteSpace($VaultAddress)) {
        $VaultAddress = Read-Host 'Indsæt Vaults HTTPS-adresse fra den projektansvarlige (uden /ui/)'
    }
    $VaultAddress = Resolve-VaultAddress -Address $VaultAddress.Trim()

    if ([string]::IsNullOrWhiteSpace($RoleId)) {
        $RoleId = Read-Host 'Indsæt backendrollens RoleID fra den projektansvarlige'
    }
    if ([string]::IsNullOrWhiteSpace($RoleId)) {
        throw 'RoleID mangler. Bed den projektansvarlige om backendrollens RoleID.'
    }

    Write-Host 'Indsæt dit personlige engangstoken. Indtastningen er skjult. Afslut med Enter.'
    $wrappingToken = Read-Host 'Engangstoken (wrapping_token)' -AsSecureString
    $secretId = $null
    $values = @{}
    $previousTls = [Net.ServicePointManager]::SecurityProtocol
    try {
        # Windows PowerShell 5.1 may otherwise negotiate obsolete TLS versions.
        [Net.ServicePointManager]::SecurityProtocol = $previousTls -bor [Net.SecurityProtocolType]::Tls12
        $secretId = Get-VaultSetupSecret -Address $VaultAddress -WrappingToken $wrappingToken
        $values = @{
            VAULT_ADDR = $VaultAddress
            VAULT_ROLE_ID = $RoleId.Trim()
            VAULT_SECRET_ID = $secretId
        }
        try {
            Save-VaultSetupEnvironment -Values $values
        }
        catch {
            throw 'SecretID blev hentet, men miljøvariablerne kunne ikke alle gemmes. Kontakt den projektansvarlige; engangstokenet er nu brugt.'
        }

        Write-Host 'Vault-opsætningen er gemt for din Windows-bruger. SecretID er ikke vist.'
        Write-Host 'Luk alle Visual Studio-vinduer, og åbn Visual Studio igen fra Start-menuen.'
        Write-Host 'Fortsæt i docs/team-setup.md. De øvrige API-indstillinger skal også være på plads.'
    }
    finally {
        [Net.ServicePointManager]::SecurityProtocol = $previousTls
        if ($null -ne $wrappingToken) { $wrappingToken.Dispose() }
        $values.Clear()
        $secretId = $null
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Initialize-VaultEnvironment @PSBoundParameters
}
