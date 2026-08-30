targetScope = 'resourceGroup'

// =============================================================================
// expensetracker019 — Azure deployment on free tiers only.
//
// Everything here is chosen to cost nothing:
//   Azure SQL Database  free offer  — 100,000 vCore-seconds + 32 GB/month, renews
//                                     monthly, never expires. ONE per subscription,
//                                     which is why both services share it under
//                                     separate schemas (auth / dbo).
//   App Service plan    F1 Free     — hosts both APIs. 60 CPU-minutes/day, 1 GB RAM,
//                                     no Always On, so expect a cold first request.
//   Static Web Apps     Free        — hosts the React SPA, with TLS included.
//
// The free SQL database is set to AUTO-PAUSE when the monthly grant runs out rather
// than bill: the app stops until the grant renews, but the bill stays at zero. Flip
// `sqlFreeLimitExhaustionBehavior` to 'BillOverUsage' only when you want uptime more
// than you want a zero bill.
//
// Aspire is a development-time orchestrator only — it is not deployed. These are
// plain App Service apps wired together with app settings.
// =============================================================================

@description('Prefix for resource names. Must be globally unique-ish: it is used in hostnames.')
@minLength(3)
@maxLength(17)
param namePrefix string = 'expensetracker019'

@description('Region for App Service and SQL. Static Web Apps has its own smaller region list.')
param location string = resourceGroup().location

@description('Region for the Static Web App. Free tier is not offered in every region.')
@allowed([
  'westus2'
  'centralus'
  'eastus2'
  'westeurope'
  'eastasia'
])
param staticWebAppLocation string = 'eastasia'

@description('Administrator login for the SQL server.')
param sqlAdminLogin string = 'sqladmin'

@description('Administrator password for the SQL server. Supply from a GitHub secret, never a file.')
@secure()
param sqlAdminPassword string

@description('Email of the seeded application administrator.')
param adminSeedEmail string

@description('Password for the seeded application administrator.')
@secure()
param adminSeedPassword string

@description('''
Base64 of a PFX holding the OpenIddict signing certificate, and its password.
Leave empty to fall back to development certificates — acceptable for a first
smoke test, but every restart then invalidates previously issued tokens.
''')
@secure()
param openIddictCertificateBase64 string = ''

@description('Password for the PFX above.')
@secure()
param openIddictCertificatePassword string = ''

@description('''
Custom domain for the SPA, e.g. app.microapps019.com. Leave empty to stay on the
generated *.azurestaticapps.net hostname.

The DNS record must already exist and resolve before you deploy with this set —
Azure validates ownership during creation and the deployment fails otherwise.
''')
param customDomain string = ''

@description('''
How Azure proves you own the domain. `cname-delegation` for a subdomain already
pointed at the site by CNAME; `dns-txt-token` for an apex domain.
''')
@allowed([
  'cname-delegation'
  'dns-txt-token'
])
param customDomainValidation string = 'cname-delegation'

@description('What the free SQL database should do once the monthly grant is used up.')
@allowed([
  'AutoPause'
  'BillOverUsage'
])
param sqlFreeLimitExhaustionBehavior string = 'AutoPause'

var authAppName = '${namePrefix}-auth'
var apiAppName = '${namePrefix}-api'
var sqlServerName = '${namePrefix}-sql'
var databaseName = 'expensetracker019'

// ---------------------------------------------------------------------------
// Database — the single free one, shared by both services via schemas.
// ---------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// App Service outbound IPs are not fixed on the free tier, so the apps reach SQL
// through the "allow Azure services" rule rather than an address list.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    // Serverless General Purpose is the only shape the free offer applies to.
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    // 32 GB is the free ceiling; asking for more silently forfeits the free grant.
    maxSizeBytes: 34359738368
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
    useFreeLimit: true
    freeLimitExhaustionBehavior: sqlFreeLimitExhaustionBehavior
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'

// ---------------------------------------------------------------------------
// Compute — one free plan carrying both APIs.
// ---------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource staticSite 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${namePrefix}-web'
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // The GitHub workflow pushes the built SPA; Azure does not build it itself.
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

// Everything downstream hangs off this: Auth019's CORS, its Spa__Origin, and the
// redirect URIs AuthSeeder registers for the SPA client. Point it at the custom
// domain or sign-in breaks — the app would load, then be refused on the way back.
var spaOrigin = empty(customDomain)
  ? 'https://${staticSite.properties.defaultHostname}'
  : 'https://${customDomain}'
resource staticSiteCustomDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = if (!empty(customDomain)) {
  parent: staticSite
  name: customDomain
  properties: {
    validationMethod: customDomainValidation
  }
}

var authUrl = 'https://${authAppName}.azurewebsites.net'
var apiUrl = 'https://${apiAppName}.azurewebsites.net'

resource authApp 'Microsoft.Web/sites@2023-12-01' = {
  name: authAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      // Always On is not available on F1; the first request after idling is slow.
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__auth019db', value: sqlConnectionString }
        // Everyone must agree on one issuer string, or the API rejects every token.
        { name: 'OpenIddict__Issuer', value: authUrl }
        { name: 'Spa__Origin', value: spaOrigin }
        { name: 'Cors__AllowedOrigins__0', value: spaOrigin }
        { name: 'AdminSeed__Email', value: adminSeedEmail }
        { name: 'AdminSeed__Password', value: adminSeedPassword }
        { name: 'OpenIddict__SigningCertificateBase64', value: openIddictCertificateBase64 }
        { name: 'OpenIddict__SigningCertificatePassword', value: openIddictCertificatePassword }
      ]
    }
  }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__expensedb', value: sqlConnectionString }
        { name: 'Auth019__Issuer', value: authUrl }
        { name: 'Cors__AllowedOrigins__0', value: spaOrigin }
      ]
    }
  }
}

output authUrl string = authUrl
output apiUrl string = apiUrl
output spaUrl string = spaOrigin
output staticSiteDefaultHostname string = staticSite.properties.defaultHostname
output authAppName string = authApp.name
output apiAppName string = apiApp.name
output staticSiteName string = staticSite.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
