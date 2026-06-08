# Infrastructure Setup Guide

This guide walks you through the one-time manual steps required before the GitHub Actions workflow can deploy Azure infrastructure via Terraform.

**Prerequisites:** Azure CLI (`az`) installed, a GitHub repo with admin access, and an Azure subscription with Owner/Contributor role.

---

## 1. Create an Azure AD App Registration for GitHub OIDC

This app registration allows GitHub Actions to authenticate to Azure without storing secrets.

```powershell
# Login to Azure
az login

# Set your subscription
az account set --subscription "c4fb1c99-fb99-4dc1-9926-a3a4356fd44a"

# Create the app registration
az ad app create --display-name "github-middleware-deploy"

# Note the appId from the output — this is your AZURE_CLIENT_ID
# Example output: "appId": "12345678-abcd-1234-abcd-123456789012"

# Create a service principal for the app
az ad sp create --id <APP_ID>
```

---

## 2. Set Up Federated Credentials for GitHub OIDC

This links the GitHub repo to the Azure app registration so GitHub Actions can request tokens.

```powershell
# Replace <APP_ID> with the appId from Step 1

# Credential for 'dev' environment
'{"name":"github-middleware-dev","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:dev","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json

# Credential for 'dev-plan' environment (plan job uses this)
'{"name":"github-middleware-dev-plan","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:dev-plan","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json

# Cleanup
Remove-Item fed-cred.json

# Repeat for staging and prod if needed (uncomment and run):
# '{"name":"github-middleware-staging","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:staging","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
# az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json
# '{"name":"github-middleware-staging-plan","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:staging-plan","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
# az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json
# '{"name":"github-middleware-prod","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:prod","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
# az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json
# '{"name":"github-middleware-prod-plan","issuer":"https://token.actions.githubusercontent.com","subject":"repo:AIS-Commercial-Business-Unit/Middleware:environment:prod-plan","audiences":["api://AzureADTokenExchange"]}' | Set-Content fed-cred.json
# az ad app federated-credential create --id 9fc6a20f-ef99-47ee-a85b-98f206add660 --parameters @fed-cred.json
# Remove-Item fed-cred.json
```

---

## 3. Grant the Service Principal Access to Your Subscription

```powershell
# Assign Owner role (Terraform needs this to create resources AND assign RBAC roles)
az role assignment create --assignee 9fc6a20f-ef99-47ee-a85b-98f206add660 --role "Owner" --scope "/subscriptions/c4fb1c99-fb99-4dc1-9926-a3a4356fd44a"
```

> **If the CLI fails with an ABAC error:** Do it via **Azure Portal → Subscriptions → c4fb1c99... → Access control (IAM) → Add → Add role assignment → Owner → Members → select `github-middleware-deploy` → Assign**

---

## 4. Create GitHub Repository Secrets

In your GitHub repository, go to **Settings → Secrets and variables → Actions** and create:

| Secret Name              | Value                                          |
|--------------------------|------------------------------------------------|
| `AZURE_CLIENT_ID`       | The `appId` from Step 1                        |
| `AZURE_TENANT_ID`       | Your Azure AD tenant ID (`az account show --query tenantId -o tsv`) |
| `AZURE_SUBSCRIPTION_ID` | Your subscription ID (`az account show --query id -o tsv`) |
| `APIM_PUBLISHER_EMAIL`  | Email address for APIM publisher notifications |

> **Note:** No `AZURE_CLIENT_SECRET` is needed — OIDC federated credentials eliminate client secrets entirely.
> **Note:** No `SQL_ADMIN_PASSWORD` secret is needed — Terraform auto-generates a random password and stores it directly in Key Vault. Your pods retrieve it via SecretProviderClass.

---

## 5. Create GitHub Environments

The workflow uses GitHub Environments for approval gates. Go to **Settings → Environments** and create:

- `dev-plan` — no protection rules (auto-approve plan)
- `dev` — add required reviewers for apply approval
- `staging-plan` — no protection rules
- `staging` — required reviewers
- `prod-plan` — no protection rules
- `prod` — required reviewers

---

## 6. Create Azure Storage Account for Terraform Remote State

Terraform stores its state file in Azure Blob Storage. Create this **before** running the workflow.

```powershell
# Create resource group for state storage
az group create --name "rg-middleware-tfstate" --location "eastus2"

# Create storage account (name must be globally unique, lowercase, no hyphens)
az storage account create --name "stmiddlewaretfstate" --resource-group "rg-middleware-tfstate" --location "eastus2" --sku "Standard_LRS" --min-tls-version "TLS1_2"

# Create blob container for state files
az storage container create --name "tfstate" --account-name "stmiddlewaretfstate"

# Grant the GitHub OIDC service principal access to the state storage
az role assignment create --assignee 9fc6a20f-ef99-47ee-a85b-98f206add660 --role "Storage Blob Data Contributor" --scope "/subscriptions/c4fb1c99-fb99-4dc1-9926-a3a4356fd44a/resourceGroups/rg-middleware-tfstate/providers/Microsoft.Storage/storageAccounts/stmiddlewaretfstate"
```

> **Note:** If the storage account name `stmiddlewaretfstate` is taken, choose another name and update `infra/terraform/main.tf` backend block accordingly.

---

## 7. Run the Workflow for the First Time

> **⚠️ First deploy takes ~45 minutes.** APIM Developer tier provisioning is the bottleneck (~40min). Subsequent runs are fast (~5min).

1. Go to your GitHub repository → **Actions** tab
2. Select **"Deploy Infrastructure"** workflow on the left
3. Click **"Run workflow"** button
4. Select `dev` as the environment
5. Click **"Run workflow"**

The workflow will:
1. **Plan job** — runs `terraform plan` and uploads the plan artifact
2. **Apply job** — waits for environment approval, then applies the plan

After the plan job completes, go to the pending deployment in the Actions UI and approve it.

---

## Troubleshooting

### "OIDC token request failed"
- Verify federated credentials match your repo name and environment exactly
- Check that the `id-token: write` permission is set in the workflow

### "Error acquiring token"
- Ensure the service principal has Contributor + User Access Administrator on the subscription
- Verify `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` are correct in GitHub secrets

### "Storage account not found" during init
- Run Step 6 first — the tfstate storage must exist before Terraform can initialize
- Verify the storage account name in `main.tf` backend block matches what you created

### State lock errors
- Another workflow run may be in progress — wait for it to complete
- If stuck, you can break the lease: `az storage blob lease break --blob-name middleware.tfstate --container-name tfstate --account-name stmiddlewaretfstate`
