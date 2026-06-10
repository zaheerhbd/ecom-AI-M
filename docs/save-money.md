# Save Money

This document explains how to reduce Azure credit usage for the eCOM-AI portfolio deployment.

## What is costing money

The main Azure resources currently using credit are:

- Azure AI Search service
- Azure Database for PostgreSQL Flexible Server
- Azure Container App
- Azure Container Registry
- Log Analytics Workspace

For this project, the biggest likely costs are usually:

1. Azure AI Search
2. PostgreSQL Flexible Server
3. Container Apps and logging
4. Azure Container Registry

## Best low-risk way to save money

If you are not actively showing the project, the safest first action is:

- stop PostgreSQL

This saves compute cost while keeping your data.

Command:

```powershell
az postgres flexible-server stop --resource-group eCom --name ecom-ai-pg-zahee01
```

Important:

- the database data stays
- the app will not work until the database is started again
- PostgreSQL Flexible Server can automatically start again after 7 days

To start it again later:

```powershell
az postgres flexible-server start --resource-group eCom --name ecom-ai-pg-zahee01
```

## Safe minimal shutdown plan

Use this if you want to save money but keep recovery simple.

### 1. Stop PostgreSQL

```powershell
az postgres flexible-server stop --resource-group eCom --name ecom-ai-pg-zahee01
```

### 2. Keep the Container App, Registry, and Search service

This keeps your deployment easier to resume later.

Tradeoff:

- you still use some Azure credit
- but much less than leaving PostgreSQL running

## Aggressive near-zero-cost shutdown plan

Use this if you want to save as much credit as possible and do not need the app live.

### 1. Stop PostgreSQL

```powershell
az postgres flexible-server stop --resource-group eCom --name ecom-ai-pg-zahee01
```

### 2. Delete the Container App

```powershell
az containerapp delete --name ecom-ai-ca-zahee01 --resource-group eCom --yes
```

Effect:

- app URL goes away
- app can be recreated later

### 3. Delete the Log Analytics Workspace

```powershell
az monitor log-analytics workspace delete --resource-group eCom --workspace-name ecom-ai-logs-zahee01 --yes
```

Effect:

- stops logging-related cost
- logs are lost

### 4. Delete Azure Container Registry if you do not need fast redeploy

```powershell
az acr delete --name ecomaiacrzahee01 --resource-group eCom --yes
```

Effect:

- stored Docker image is deleted
- later redeploy requires building and pushing the image again

## What to keep and what to remove

Keep if you want easy recovery:

- resource group
- Azure AI Search
- PostgreSQL data

Stop or remove if you want lower cost:

- stop PostgreSQL
- delete Container App
- delete Log Analytics Workspace
- delete Azure Container Registry

## Recommended student strategy

For a portfolio project:

- keep the app down most of the time
- start or recreate only before demos
- keep screenshots and a short demo video

That way, you do not need to keep Azure services running all month.

## Suggested action right now

If you want the safest money-saving move without losing data, run:

```powershell
az postgres flexible-server stop --resource-group eCom --name ecom-ai-pg-zahee01
```

That is the best first step.
