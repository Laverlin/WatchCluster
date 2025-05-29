#!/bin/bash

echo "This script copies secrets into the deploy-k8s/base directories and updates .gitignore."
echo "It should be run only once after cloning the repository."
echo

# Define arrays of source and destination directories
SOURCE_DIRS=(
  "$HOME/Sync/Projects/AppSecrets/watch-cluster"
  "$HOME/Sync/Projects/AppSecrets/watch-cluster/dev"
  "$HOME/Sync/Projects/AppSecrets/watch-cluster/prod"
  # Add more source paths here
)
DEST_DIRS=(
  "$(pwd)/setup/base"
  "$(pwd)/setup/overlays/proxmox"
  "$(pwd)/setup/overlays/hetzner"
  # Add corresponding destination paths here
)

# Check that arrays have the same length
if [ "${#SOURCE_DIRS[@]}" -ne "${#DEST_DIRS[@]}" ]; then
  echo -e "\033[31m❌ Error: SOURCE_DIRS and DEST_DIRS length mismatch.\033[0m"
  exit 1
fi

# Loop through each pair and copy secrets
for i in "${!SOURCE_DIRS[@]}"; do
  SRC="${SOURCE_DIRS[$i]}"
  DST="${DEST_DIRS[$i]}"

  echo -e "\nLinking secrets from $SRC to $DST"
  # Ensure the source directory exists
  if [ ! -d "$SRC" ]; then
    echo -e "\033[31m❌ Source directory $SRC does not exist.\033[0m"
    exit 1
  fi
  # Create destination directory if needed
  mkdir -p "$DST"

  # Create symlinks for files with '.secret' in their name, including hidden files
  shopt -s dotglob
  for file in "$SRC"/*.secret*; do
    # Skip if no files match the pattern
    [ -e "$file" ] || continue
    filename=$(basename "$file")
    cp -f "$file" "$DST/$filename"
    echo -e "\033[32m📋 Copied $filename to $DST\033[0m"
  done
done



# Check if .gitignore exists in the root directory
GITIGNORE_FILE="$(pwd)/.gitignore"
if [ ! -f "$GITIGNORE_FILE" ]; then
  echo "# .gitignore file" > "$GITIGNORE_FILE"
  echo -e "\033[32mCreated .gitignore file.\033[0m"
fi

# Check if *.secret* pattern exists in .gitignore
if ! grep -q "*.secret*" "$GITIGNORE_FILE"; then
  echo "*.secret*" >> "$GITIGNORE_FILE"
  echo -e "\033[32mAdded *.secret* to .gitignore.\033[0m"
else
  echo -e "*.secret* already exists in .gitignore."
fi

echo -e "\033[32m✅ Secrets linking and .gitignore update completed.\033[0m"
