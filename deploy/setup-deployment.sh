#!/usr/bin/env bash
# deploy all secrets and argocd applications to a specific cluster environment

set -euo pipefail

echo "This script deploys secrets and argocd applications to a specific cluster environment."


if [ "${1-}" == "" ]; then
  echo -e "\033[33miCluster name is required as the first argument.\033[0m"
  echo "Usage: $0 <cluster-name> [-rdb]"
  echo "  <cluster-name>  Name of the cluster environment (e.g., proxmox, hetzner)"
  echo "  -rdb             Deploy database"
  echo "Example: $0 proxmox -rdb"
  exit 1
fi

# Define variables
KUBECONFIG="$HOME/remote-kube/$1/config"
BASE_DIR="$(dirname "$0")/setup-deployment"

if [ ! -f "$KUBECONFIG" ]; then
  echo -e "\033[31m❌ Kubeconfig file not found at $KUBECONFIG.\033[0m"
  exit 1
fi

# Check if setup-deployment symlink exists, create if needed
SECRET_SOURCE_DIR="$HOME/Sync/Projects/AppSecrets/watch-cluster/setup-deployment"

if [ ! -L "$BASE_DIR" ] && [ ! -d "$BASE_DIR" ]; then
  if [ -d "$SECRET_SOURCE_DIR" ]; then
    echo "🔗 Creating symlink to setup-deployment from secrets directory..."
    ln -sf "$SECRET_SOURCE_DIR" "$BASE_DIR"
    echo -e "\033[32m✅ Symlink created: $BASE_DIR -> $SECRET_SOURCE_DIR\033[0m"
  else
    echo -e "\033[31m❌ Secret source directory not found at $SECRET_SOURCE_DIR.\033[0m"
    exit 1
  fi
elif [ ! -L "$BASE_DIR" ] && [ -d "$BASE_DIR" ]; then
  echo -e "\033[33m⚠️  setup-deployment directory exists but is not a symlink.\033[0m"
fi

# Check if the -rdb flag is provided
if [[ "${2-}" == "-rdb" ]]; then
  echo "Deploying database..."
  kubectl apply --kubeconfig "$KUBECONFIG" -f "$BASE_DIR/restore-db.yaml"
else
  # Deploy secrets & argocd applications
  echo "🚀 Deploying secrets and argocd applications to $1 environment..."
  kubectl apply --kubeconfig "$KUBECONFIG" -k "$BASE_DIR/overlays/$1"
fi

echo "Deployment complete."
