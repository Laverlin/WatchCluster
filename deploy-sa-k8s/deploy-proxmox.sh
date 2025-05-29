#!/usr/bin/env bash
# deploy-dev.sh: deploy the dev overlay via kubectl

set -euo pipefail


# Define variables
KUBECONFIG="$HOME/remote-kube/proxmox/config"
BASE_DIR="$(dirname "$0")"

echo "🚀 Deploying complete proxmox environment..."
kubectl apply --kubeconfig "$KUBECONFIG" -k "$BASE_DIR/overlays/proxmox"

echo "Deployment complete."
