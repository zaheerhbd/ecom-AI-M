param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$Suffix,

    [Parameter(Mandatory = $true)]
    [string]$PostgresPassword,

    [string]$PostgresAdminUser = "appuser",
    [string]$AppServicePlan = "ecom-ai-plan",
    [string]$ImageName = "ecom-ai",
    [string]$ImageTag = "latest"
)

$ErrorActionPreference = "Stop"

$acrName = ("ecomaiacr" + $Suffix).ToLower()
$webAppName = ("ecom-ai-app-" + $Suffix).ToLower()
$postgresServer = ("ecom-ai-pg-" + $Suffix).ToLower()
$image = "$acrName.azurecr.io/$ImageName`:$ImageTag"
$appHost = "https://$webAppName.azurewebsites.net"
$pgHost = "$postgresServer.postgres.database.azure.com"

Write-Host "Creating resource group..." -ForegroundColor Cyan
az group create --name $ResourceGroup --location $Location | Out-Null

Write-Host "Creating container registry..." -ForegroundColor Cyan
az acr create --resource-group $ResourceGroup --name $acrName --sku Basic | Out-Null

Write-Host "Creating App Service plan..." -ForegroundColor Cyan
az appservice plan create --name $AppServicePlan --resource-group $ResourceGroup --is-linux --sku B1 | Out-Null

Write-Host "Creating Web App..." -ForegroundColor Cyan
az webapp create --resource-group $ResourceGroup --plan $AppServicePlan --name $webAppName --deployment-container-image-name $image | Out-Null

Write-Host "Creating PostgreSQL flexible server..." -ForegroundColor Cyan
az postgres flexible-server create `
  --resource-group $ResourceGroup `
  --name $postgresServer `
  --location $Location `
  --admin-user $PostgresAdminUser `
  --admin-password $PostgresPassword `
  --sku-name Standard_B1ms `
  --tier Burstable `
  --storage-size 32 `
  --version 14 `
  --public-access 0.0.0.0 | Out-Null

Write-Host "Creating application databases..." -ForegroundColor Cyan
az postgres flexible-server db create --resource-group $ResourceGroup --server-name $postgresServer --database-name skinet | Out-Null
az postgres flexible-server db create --resource-group $ResourceGroup --server-name $postgresServer --database-name identity | Out-Null

Write-Host "Logging into ACR..." -ForegroundColor Cyan
az acr login --name $acrName

Write-Host "Building container image..." -ForegroundColor Cyan
docker build -t $image .

Write-Host "Pushing container image..." -ForegroundColor Cyan
docker push $image

$acrUser = az acr credential show --name $acrName --query username -o tsv
$acrPass = az acr credential show --name $acrName --query "passwords[0].value" -o tsv

Write-Host "Configuring Web App container..." -ForegroundColor Cyan
az webapp config container set `
  --name $webAppName `
  --resource-group $ResourceGroup `
  --docker-custom-image-name $image `
  --docker-registry-server-url "https://$acrName.azurecr.io" `
  --docker-registry-server-user $acrUser `
  --docker-registry-server-password $acrPass | Out-Null

Write-Host "Configuring app settings..." -ForegroundColor Cyan
az webapp config appsettings set `
  --resource-group $ResourceGroup `
  --name $webAppName `
  --settings `
  ASPNETCORE_ENVIRONMENT=Production `
  WEBSITES_PORT=8080 `
  RUN_MIGRATIONS=false `
  "ConnectionStrings__DefaultConnection=Host=$pgHost;Port=5432;Database=skinet;Username=$PostgresAdminUser;Password=$PostgresPassword;Ssl Mode=Require;Trust Server Certificate=true" `
  "ConnectionStrings__IdentityConnection=Host=$pgHost;Port=5432;Database=identity;Username=$PostgresAdminUser;Password=$PostgresPassword;Ssl Mode=Require;Trust Server Certificate=true" `
  "Token__Key=replace-with-a-long-random-secret-at-least-32-characters" `
  "Token__Issuer=$appHost" `
  "ApiUrl=$appHost/Content/" `
  "Cors__AllowedOrigins__0=$appHost" | Out-Null

Write-Host ""
Write-Host "Deployment resources created." -ForegroundColor Green
Write-Host "Web app: $appHost"
Write-Host "Swagger: $appHost/swagger"
Write-Host ""
Write-Host "Next step: run migrations once with:" -ForegroundColor Yellow
Write-Host "az webapp config appsettings set --resource-group $ResourceGroup --name $webAppName --settings RUN_MIGRATIONS=true"
Write-Host "az webapp restart --resource-group $ResourceGroup --name $webAppName"
Write-Host ""
Write-Host "Then turn migrations back off with:" -ForegroundColor Yellow
Write-Host "az webapp config appsettings set --resource-group $ResourceGroup --name $webAppName --settings RUN_MIGRATIONS=false"
