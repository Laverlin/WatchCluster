#!/usr/bin/env bash
# deploy-proxmox.sh: deploy the proxmox overlay via kubectl

set -euo pipefail


# Define variables
KUBECONFIG="$HOME/remote-kube/proxmox/config"
BASE_DIR="$(dirname "$0")"

# Check if "db-restore" parameter is passed
if [ "${1-}" = "db-restore" ]; then
  echo "📊 Applying database restore job only..."
  kubectl apply --kubeconfig "$KUBECONFIG" -f "$BASE_DIR/base/restore-db.yaml"
else
  echo "🚀 Deploying complete proxmox environment..."
  kubectl apply --kubeconfig "$KUBECONFIG" -k "$BASE_DIR/overlays/proxmox"

  echo -e "\033[33mif you want to restore the database, run the following command: ./deploy-proxmox.sh db-restore\033[0m"
fi

echo "Deployment complete."
