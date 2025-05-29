#!/usr/bin/env bash
# deploy-proxmox.sh: deploy the proxmox overlay via kubectl

set -euo pipefail


# Define variables
KUBECONFIG="$HOME/remote-kube/$1/config"
BASE_DIR="$(dirname "$0")/deploy-secrets"

if [ ! "${1}" ]; then
  echo -e "\033[33miCluster name is required as the first argument.\033[0m"
  echo "Usage: $0 <cluster-name>"
  exit 1
fi

echo "🚀 Deploying secrets to $1 environment..."
kubectl apply --kubeconfig "$KUBECONFIG" -k "$BASE_DIR/overlays/$1"


echo "Deployment complete."
