#!/usr/bin/env bash
# deploy all secrets and argocd applications to a specific cluster environment

set -euo pipefail

echo "This script deploys database, secrets and argocd applications to a specific cluster environment."


if [ "${1-}" == "" ]; then
  echo -e "\033[33miCluster name is required as the first argument.\033[0m"
  echo "Usage: $0 <cluster-name> [-rdb]"
  echo "  <cluster-name>  Name of the cluster environment (e.g., proxmox, hetzner)"
  echo "  -rdb            Deploy database from azure backup"
  echo "Example: $0 proxmox -rdb"
  exit 1
fi

# Define variables
KUBECONFIG="$HOME/remote-kube/$1/config"
BASE_DIR="$(dirname "$0")/setup-deployment"
TEMP_DEPLOY_DIR="$HOME/projects/deploy/wf-deploy"

if [ ! -f "$KUBECONFIG" ]; then
  echo -e "\033[31m❌ Kubeconfig file not found at $KUBECONFIG.\033[0m"
  exit 1
fi

# Function to clean up temp directory
cleanup() {
  if [ -d "$TEMP_DEPLOY_DIR" ]; then
    echo "🧹 Cleaning up temporary deployment directory..."
    rm -rf "$TEMP_DEPLOY_DIR"
  fi
}

# Set trap to ensure cleanup happens on exit
trap cleanup EXIT

# Create temporary deployment directory and copy contents
echo "📁 Creating temporary deployment directory at $TEMP_DEPLOY_DIR..."
mkdir -p "$TEMP_DEPLOY_DIR"
cp -r "$BASE_DIR"/* "$TEMP_DEPLOY_DIR/"

# Decrypt SOPS files
echo "🔓 Decrypting SOPS files..."
find "$TEMP_DEPLOY_DIR" -name "*.sops.*" -not -name "*.decrypted.*" | while read -r sops_file; do
  # Extract the decrypted filename by removing .sops from the name
  decrypted_file="${sops_file//.sops/}"
  echo "🔐 Decrypting $sops_file -> $decrypted_file"
  sops -d "$sops_file" > "$decrypted_file"
done



# Deploy secrets & argocd applications
echo "🚀 Deploying secrets and argocd applications to $1 environment..."
kubectl apply --kubeconfig "$KUBECONFIG" -k "$TEMP_DEPLOY_DIR/overlays/$1"

# Check if the -rdb flag is provided
if [[ "${2-}" == "-rdb" ]]; then
  echo "Deploying database..."
  kubectl apply --kubeconfig "$KUBECONFIG" -f "$TEMP_DEPLOY_DIR/restore-azure-db.yaml"
fi

echo "Deployment complete."
