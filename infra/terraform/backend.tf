# Remote state in the bootstrap storage account.
#
# PARTIAL CONFIGURATION, deliberately: `key` is NOT set here so that dev and
# prod cannot share a state file. A single hardcoded key would let a dev
# apply overwrite production infrastructure.
#
# Initialise per environment:
#
#   terraform init -reconfigure \
#     -backend-config=envs/dev.backend.hcl \
#     -backend-config="storage_account_name=$TFSTATE_STORAGE_ACCOUNT"
#
# Swap dev for prod to target production.
#
terraform {
  backend "azurerm" {}
}
