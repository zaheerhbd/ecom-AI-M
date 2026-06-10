# Azure Deployment Guide

This repo is easiest to deploy to Azure as a single container:

- Angular builds into `API/wwwroot`
- ASP.NET Core serves both the API and SPA
- PostgreSQL stays as a managed Azure service

That avoids split-host CORS issues and avoids relying on Azure's retired `.NET 5` built-in runtime.

## Architecture

- App: Azure Web App for Containers
- Database: Azure Database for PostgreSQL Flexible Server
- Optional AI services:
  - Azure AI Search
  - OpenAI or Azure OpenAI

## 1. Prerequisites

- Azure CLI installed
- Docker installed locally
- An Azure subscription
- A unique suffix for globally unique resource names

Example values used below:

```powershell
$LOCATION = "eastus"
$RG = "ecom-ai-rg"
$SUFFIX = "replace123"
$ACR = "ecomaiacr$SUFFIX"
$PLAN = "ecom-ai-plan"
$APP = "ecom-ai-app-$SUFFIX"
$PG = "ecom-ai-pg-$SUFFIX"
$IMAGE = "$ACR.azurecr.io/ecom-ai:latest"
```

## 2. Create Azure resources

```powershell
az login
az group create --name $RG --location $LOCATION
az acr create --resource-group $RG --name $ACR --sku Basic
az appservice plan create --name $PLAN --resource-group $RG --is-linux --sku B1
az webapp create --resource-group $RG --plan $PLAN --name $APP --deployment-container-image-name $IMAGE
```

### PostgreSQL

```powershell
az postgres flexible-server create `
  --resource-group $RG `
  --name $PG `
  --location $LOCATION `
  --sku-name Standard_B1ms `
  --tier Burstable `
  --admin-user appuser `
  --admin-password "ChangeThisPassword123!" `
  --storage-size 32 `
  --version 14

az postgres flexible-server db create --resource-group $RG --server-name $PG --database-name skinet
az postgres flexible-server db create --resource-group $RG --server-name $PG --database-name identity
```

## 3. Build and push the container

From the repo root:

```powershell
az acr login --name $ACR
docker build -t $IMAGE .
docker push $IMAGE
```

## 4. Configure app settings

Use Azure App Settings instead of committing production secrets.

```powershell
$PGHOST = "$PG.postgres.database.azure.com"
$APP_HOST = "https://$APP.azurewebsites.net"

az webapp config appsettings set `
  --resource-group $RG `
  --name $APP `
  --settings `
  ASPNETCORE_ENVIRONMENT=Production `
  WEBSITES_PORT=8080 `
  RUN_MIGRATIONS=false `
  "ConnectionStrings__DefaultConnection=Host=$PGHOST;Port=5432;Database=skinet;Username=appuser;Password=ChangeThisPassword123!;Ssl Mode=Require;Trust Server Certificate=true" `
  "ConnectionStrings__IdentityConnection=Host=$PGHOST;Port=5432;Database=identity;Username=appuser;Password=ChangeThisPassword123!;Ssl Mode=Require;Trust Server Certificate=true" `
  "Token__Key=replace-with-a-long-random-secret-at-least-32-characters" `
  "Token__Issuer=$APP_HOST" `
  "ApiUrl=$APP_HOST/Content/" `
  "Cors__AllowedOrigins__0=$APP_HOST" `
  "StripeSettings__PublishableKey=" `
  "StripeSettings__SecretKey=" `
  "StripeSettings__WhSecret=" `
  "OpenAI__ApiKey=" `
  "OpenAI__BaseUrl=https://api.openai.com/" `
  "OpenAI__ChatModel=gpt-5.4-mini" `
  "OpenAI__EmbeddingModel=text-embedding-3-small" `
  "AzureSearch__Endpoint=" `
  "AzureSearch__ApiKey=" `
  "AzureSearch__IndexName=products-index" `
  "AzureSearch__RagIndexName=rag-index"
```

Notes:

- If the app will be served only from the same hostname, keeping `Cors__AllowedOrigins__0` equal to `$APP_HOST` is enough.
- If you later split frontend and API onto different hosts, add more `Cors__AllowedOrigins__N` values.

## 5. Point the app at the container registry

```powershell
$ACR_USER = az acr credential show --name $ACR --query username -o tsv
$ACR_PASS = az acr credential show --name $ACR --query "passwords[0].value" -o tsv

az webapp config container set `
  --resource-group $RG `
  --name $APP `
  --container-image-name $IMAGE `
  --container-registry-url "https://$ACR.azurecr.io" `
  --container-registry-user $ACR_USER `
  --container-registry-password $ACR_PASS
```

## 6. Run database migrations once

This app supports one-off migration execution with `RUN_MIGRATIONS=true`.

Set it to `true`, restart the app once, then set it back to `false`.

```powershell
az webapp config appsettings set --resource-group $RG --name $APP --settings RUN_MIGRATIONS=true
az webapp restart --resource-group $RG --name $APP
```

After the site starts successfully, turn it back off:

```powershell
az webapp config appsettings set --resource-group $RG --name $APP --settings RUN_MIGRATIONS=false
```

Optional one-time startup jobs:

- `INIT_SEARCH_INDEX=true`
- `SYNC_PRODUCTS_TO_AZURE_SEARCH=true`
- `SYNC_RAG_TO_AZURE_SEARCH=true`

Use the same pattern: enable, restart, then disable.

## 7. Verify

Check these URLs:

- `https://YOUR_APP.azurewebsites.net/`
- `https://YOUR_APP.azurewebsites.net/swagger`
- `https://YOUR_APP.azurewebsites.net/api/products`

Stream logs if startup fails:

```powershell
az webapp log config --resource-group $RG --name $APP --docker-container-logging filesystem
az webapp log tail --resource-group $RG --name $APP
```

## Important notes for this repo

- The app targets `.NET 5`, so container deployment is the safest Azure option without first upgrading the backend.
- The Angular production build already outputs into `API/wwwroot`.
- Production secrets should live in Azure App Settings or Key Vault, not in `appsettings.Production.json`.
- Redis has been replaced with native in-memory storage for baskets and response caching to keep Azure costs down for portfolio use.
- The repo currently has local-only connection strings in `API/appsettings.json`; those are fine for development and do not need to be edited for Azure.

## Recommended next step

After the first manual deployment works, add CI/CD:

- GitHub Actions to build and push the image to ACR
- Azure Web App deployment on push to `main`
