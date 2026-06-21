param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5099"
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Token,
        [object]$Body,
        [int[]]$ExpectedStatusCodes = @(200)
    )

    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $requestParams = @{
        Method      = $Method
        Uri         = "$ApiBaseUrl$Path"
        Headers     = $headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $requestParams["Body"] = ($Body | ConvertTo-Json -Depth 20)
    }

    try {
        $response = Invoke-WebRequest @requestParams
        $statusCode = [int]$response.StatusCode
        $content = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json }
    }
    catch {
        $exception = $_.Exception
        $response = $exception.Response
        if (-not $response) {
            throw
        }

        $statusCode = [int]$response.StatusCode
        $stream = $response.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        $raw = $reader.ReadToEnd()
        $reader.Dispose()
        $stream.Dispose()
        $content = if ([string]::IsNullOrWhiteSpace($raw)) { $null } else {
            try { $raw | ConvertFrom-Json } catch { $raw }
        }
    }

    if ($ExpectedStatusCodes -notcontains $statusCode) {
        throw "Expected status $($ExpectedStatusCodes -join ', ') but got $statusCode for $Method $Path. Response: $($content | ConvertTo-Json -Depth 20 -Compress)"
    }

    [pscustomobject]@{
        StatusCode = $statusCode
        Body       = $content
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected' but got '$Actual'."
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$stamp = [DateTime]::UtcNow.ToString("yyyyMMddHHmmss")
$tenantEmail = "admin.$stamp@ecosys-smoke.local"
$technicianEmail = "tech.$stamp@ecosys-smoke.local"
$password = "Password123!"
$superAdminEmail = "superadmin@ecosys.local"
$superAdminPassword = "SuperAdmin123!"

Write-Host "1. Signup tenant Admin"
$signup = Invoke-Api -Method "POST" -Path "/api/auth/signup" -Body @{
    fullName    = "Smoke Admin"
    email       = $tenantEmail
    password    = $password
    companyName = "Smoke Tenant $stamp"
    industry    = "Maintenance"
    country     = "Kenya"
}
$adminToken = $signup.Body.token
$tenantId = $signup.Body.tenantId

Write-Host "2. Login Admin"
$login = Invoke-Api -Method "POST" -Path "/api/auth/login" -Body @{
    email    = $tenantEmail
    password = $password
}
Assert-Equal $login.Body.user.role "Admin" "Signup should create an Admin account"
$adminToken = $login.Body.token

Write-Host "3. Create branches Kericho and Nairobi"
$kerichoBranch = Invoke-Api -Method "POST" -Path "/api/branches" -Token $adminToken -Body @{
    name      = "Kericho Factory"
    code      = "KER"
    location  = "Kericho"
    address   = "Kericho Industrial Park"
    isActive  = $true
} -ExpectedStatusCodes @(201)

$nairobiBranch = Invoke-Api -Method "POST" -Path "/api/branches" -Token $adminToken -Body @{
    name      = "Nairobi Depot"
    code      = "NBO"
    location  = "Nairobi"
    address   = "Nairobi Inland Depot"
    isActive  = $true
} -ExpectedStatusCodes @(201)

Write-Host "4. Create numbering rules per branch"
$numberingRules = @(
    @{ branchId = $kerichoBranch.Body.id; documentType = "WorkOrder"; prefix = "KER-WO" },
    @{ branchId = $nairobiBranch.Body.id; documentType = "WorkOrder"; prefix = "NBO-WO" },
    @{ branchId = $kerichoBranch.Body.id; documentType = "MaterialRequest"; prefix = "KER-MR" },
    @{ branchId = $nairobiBranch.Body.id; documentType = "MaterialRequest"; prefix = "NBO-MR" },
    @{ branchId = $kerichoBranch.Body.id; documentType = "StockTransfer"; prefix = "KER-ST" },
    @{ branchId = $nairobiBranch.Body.id; documentType = "StockTransfer"; prefix = "NBO-ST" }
)

foreach ($rule in $numberingRules) {
    [void](Invoke-Api -Method "PUT" -Path "/api/settings/numbering" -Token $adminToken -Body @{
        branchId       = $rule.branchId
        documentType   = $rule.documentType
        prefix         = $rule.prefix
        nextNumber     = 1
        paddingLength  = 6
        resetFrequency = "Never"
        includeYear    = $false
        includeMonth   = $false
        isActive       = $true
    })
}

Write-Host "5. Create client and branch-specific assets"
$client = Invoke-Api -Method "POST" -Path "/api/clients" -Token $adminToken -Body @{
    clientName    = "Smoke Client"
    clientType    = "Corporate"
    email         = "client.$stamp@ecosys-smoke.local"
    phone         = "0700001000"
    location      = "Kenya"
    contactPerson = "Client Ops"
    contactPhone  = "0700001001"
    notes         = "Created by smoke test"
} -ExpectedStatusCodes @(201)

$kerichoAsset = Invoke-Api -Method "POST" -Path "/api/assets" -Token $adminToken -Body @{
    clientId               = $client.Body.id
    branchId               = $kerichoBranch.Body.id
    assetName              = "Kericho Boiler"
    assetCode              = "KER-AST-$stamp"
    assetType              = "Boiler"
    location               = "Kericho"
    serialNumber           = "KER-$stamp"
    manufacturer           = "Acme"
    model                  = "K-100"
    installationDate       = (Get-Date).ToUniversalTime().AddMonths(-6).ToString("o")
    warrantyExpiryDate     = (Get-Date).ToUniversalTime().AddYears(1).ToString("o")
    recommendedPmFrequency = "Monthly"
    autoSchedulePm         = $true
    nextPmDate             = (Get-Date).ToUniversalTime().AddDays(14).ToString("o")
    notes                  = "Kericho asset"
    status                 = "Active"
} -ExpectedStatusCodes @(201)

$nairobiAsset = Invoke-Api -Method "POST" -Path "/api/assets" -Token $adminToken -Body @{
    clientId               = $client.Body.id
    branchId               = $nairobiBranch.Body.id
    assetName              = "Nairobi Conveyor"
    assetCode              = "NBO-AST-$stamp"
    assetType              = "Conveyor"
    location               = "Nairobi"
    serialNumber           = "NBO-$stamp"
    manufacturer           = "Acme"
    model                  = "N-200"
    installationDate       = (Get-Date).ToUniversalTime().AddMonths(-3).ToString("o")
    warrantyExpiryDate     = (Get-Date).ToUniversalTime().AddYears(1).ToString("o")
    recommendedPmFrequency = "Quarterly"
    autoSchedulePm         = $false
    nextPmDate             = $null
    notes                  = "Nairobi asset"
    status                 = "Active"
} -ExpectedStatusCodes @(201)

Write-Host "6. Create material"
$material = Invoke-Api -Method "POST" -Path "/api/materials" -Token $adminToken -Body @{
    itemCode       = "MAT-$stamp"
    itemName       = "Smoke Filter"
    category       = "Consumables"
    unitOfMeasure  = "Piece"
    quantityOnHand = 0
    reorderLevel   = 10
    unitCost       = 120.0
    branchId       = $null
} -ExpectedStatusCodes @(201)

Write-Host "7. Replenish material in Kericho"
$kerichoStock = Invoke-Api -Method "POST" -Path "/api/materials/$($material.Body.id)/replenish" -Token $adminToken -Body @{
    branchId        = $kerichoBranch.Body.id
    quantity        = 50
    unitCost        = 120.0
    reason          = "Supplier delivery Kericho"
    referenceNumber = "KER-DN-$stamp"
}
Assert-Equal ([decimal]$kerichoStock.Body.quantityOnHand) ([decimal]50) "Kericho replenish should set branch stock to 50"

Write-Host "8. Replenish material in Nairobi"
$nairobiStock = Invoke-Api -Method "POST" -Path "/api/materials/$($material.Body.id)/replenish" -Token $adminToken -Body @{
    branchId        = $nairobiBranch.Body.id
    quantity        = 30
    unitCost        = 125.0
    reason          = "Supplier delivery Nairobi"
    referenceNumber = "NBO-DN-$stamp"
}
Assert-Equal ([decimal]$nairobiStock.Body.quantityOnHand) ([decimal]30) "Nairobi replenish should set branch stock to 30"

Write-Host "9. Confirm branch stock balances are separate"
$kerichoMaterial = Invoke-Api -Method "GET" -Path "/api/materials/$($material.Body.id)?branchId=$($kerichoBranch.Body.id)" -Token $adminToken
$nairobiMaterial = Invoke-Api -Method "GET" -Path "/api/materials/$($material.Body.id)?branchId=$($nairobiBranch.Body.id)" -Token $adminToken
Assert-Equal ([decimal]$kerichoMaterial.Body.quantityOnHand) ([decimal]50) "Kericho branch stock should remain isolated"
Assert-Equal ([decimal]$nairobiMaterial.Body.quantityOnHand) ([decimal]30) "Nairobi branch stock should remain isolated"

Write-Host "10. Create WorkOrder in Kericho and confirm KER-WO numbering"
$kerichoWorkOrder = Invoke-Api -Method "POST" -Path "/api/workorders" -Token $adminToken -Body @{
    clientId               = $client.Body.id
    branchId               = $kerichoBranch.Body.id
    assetId                = $kerichoAsset.Body.id
    title                  = "Inspect Kericho boiler"
    description            = "Kericho branch work order"
    priority               = "High"
    dueDate                = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
    isPreventiveMaintenance = $false
} -ExpectedStatusCodes @(201)
Assert-True ($kerichoWorkOrder.Body.workOrderNumber -like "KER-WO-*") "Kericho work order should use KER-WO numbering"

Write-Host "11. Create WorkOrder in Nairobi and confirm NBO-WO numbering"
$nairobiWorkOrder = Invoke-Api -Method "POST" -Path "/api/workorders" -Token $adminToken -Body @{
    clientId               = $client.Body.id
    branchId               = $nairobiBranch.Body.id
    assetId                = $nairobiAsset.Body.id
    title                  = "Inspect Nairobi conveyor"
    description            = "Nairobi branch work order"
    priority               = "Medium"
    dueDate                = (Get-Date).ToUniversalTime().AddDays(3).ToString("o")
    isPreventiveMaintenance = $false
} -ExpectedStatusCodes @(201)
Assert-True ($nairobiWorkOrder.Body.workOrderNumber -like "NBO-WO-*") "Nairobi work order should use NBO-WO numbering"

Write-Host "12. Create User with Technician job title assigned to Kericho only"
$technicianUser = Invoke-Api -Method "POST" -Path "/api/users" -Token $adminToken -Body @{
    fullName           = "John Technician"
    email              = $technicianEmail
    password           = $password
    role               = "User"
    jobTitle           = "Technician"
    department         = "Maintenance"
    permissions        = $null
    branchIds          = @($kerichoBranch.Body.id)
    defaultBranchId    = $kerichoBranch.Body.id
    hasAllBranchAccess = $false
} -ExpectedStatusCodes @(201)
Assert-Equal $technicianUser.Body.branchIds.Count 1 "Technician should have one branch assignment"

Write-Host "13. Login Technician"
$technicianLogin = Invoke-Api -Method "POST" -Path "/api/auth/login" -Body @{
    email    = $technicianEmail
    password = $password
}
$technicianToken = $technicianLogin.Body.token

Write-Host "14. Technician can see Kericho records only"
$technicianWorkOrders = Invoke-Api -Method "GET" -Path "/api/workorders" -Token $technicianToken
Assert-Equal $technicianWorkOrders.Body.Count 1 "Technician should only see one branch-scoped work order"
Assert-Equal $technicianWorkOrders.Body[0].branchId $kerichoBranch.Body.id "Technician should only see Kericho branch records"

Write-Host "15. Technician cannot access Nairobi records"
[void](Invoke-Api -Method "GET" -Path "/api/workorders?branchId=$($nairobiBranch.Body.id)" -Token $technicianToken -ExpectedStatusCodes @(403))
[void](Invoke-Api -Method "GET" -Path "/api/workorders/$($nairobiWorkOrder.Body.id)" -Token $technicianToken -ExpectedStatusCodes @(404))

Write-Host "16. Logout Technician"
[void](Invoke-Api -Method "POST" -Path "/api/auth/logout" -Token $technicianToken -Body $null)

Write-Host "17. SuperAdmin endpoint shows session counts and logout metadata"
$superAdminLogin = Invoke-Api -Method "POST" -Path "/api/auth/login" -Body @{
    email    = $superAdminEmail
    password = $superAdminPassword
}
$superAdminToken = $superAdminLogin.Body.token

$tenantSessions = Invoke-Api -Method "GET" -Path "/api/platform/tenants/$tenantId/sessions" -Token $superAdminToken
$technicianSession = $tenantSessions.Body | Where-Object { $_.email -eq $technicianEmail } | Select-Object -First 1
Assert-True ($null -ne $technicianSession) "SuperAdmin should see the technician session metadata"
Assert-True ($null -ne $technicianSession.logoutAt) "Technician LogoutAt should be set after logout"
Assert-Equal ([bool]$technicianSession.isActive) $false "Logged out technician session should not be active"

$activeCount = Invoke-Api -Method "GET" -Path "/api/platform/tenants/$tenantId/sessions/active-count" -Token $superAdminToken
Assert-True ([int]$activeCount.Body.loggedInToday -ge 2) "Tenant should show logged in users for the day"

$platformSummary = Invoke-Api -Method "GET" -Path "/api/platform/summary" -Token $superAdminToken
Assert-True ([int]$platformSummary.Body.totalTenants -ge 1) "Platform summary should include at least one tenant"
Assert-True ([int]$platformSummary.Body.activeUsersNow -ge 1) "Platform summary should show active users now"

Write-Host "Smoke test completed successfully for tenant $tenantId"
