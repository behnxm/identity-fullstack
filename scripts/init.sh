#!/usr/bin/env bash
set -euo pipefail

# Ensure we run from repository root
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Initializing Identity Fullstack local environment..."

# 1. Create secrets directory
mkdir -p "$REPO_ROOT/secrets"

# 2. Generate ES256 ECDSA P-256 private key if not already existing
KEY_FILE="$REPO_ROOT/secrets/signing-key.pem"
if [ ! -f "$KEY_FILE" ]; then
    echo "==> Generating ES256 (prime256v1 / P-256) ECDSA signing key..."
    openssl ecparam -name prime256v1 -genkey -noout -out "$KEY_FILE"
    chmod 600 "$KEY_FILE"
    echo "==> Saved private key to secrets/signing-key.pem"
else
    echo "==> Signing key already exists at secrets/signing-key.pem"
fi

# 3. Generate secrets for .env if .env doesn't exist
ENV_FILE="$REPO_ROOT/.env"
if [ ! -f "$ENV_FILE" ]; then
    echo "==> Generating .env from .env.example..."
    DB_PASSWORD=$(openssl rand -hex 16)
    DEMO_PASSWORD=$(openssl rand -base64 18 | tr -dc 'a-zA-Z0-9!@#$' | head -c 16)
    SESSION_PASSWORD=$(openssl rand -hex 24)

    sed \
        -e "s/replace_with_strong_random_db_password/$DB_PASSWORD/g" \
        -e "s/replace_with_strong_random_demo_password/$DEMO_PASSWORD/g" \
        -e "s/replace_with_at_least_32_characters_session_seal_secret/$SESSION_PASSWORD/g" \
        "$REPO_ROOT/.env.example" > "$ENV_FILE"

    chmod 600 "$ENV_FILE"

    echo "==> Generated .env with strong local credentials."
    echo ""
    echo "================================================================="
    echo "                  DEMO CREDENTIALS INITIALIZED                   "
    echo "================================================================="
    echo "  Admin User:   admin@demo.test"
    echo "  Support User: support@demo.test"
    echo "  Standard User: user@demo.test"
    echo "  Password:     $DEMO_PASSWORD"
    echo "================================================================="
    echo "Note: The demo password has been saved to your local .env file."
    echo ""
else
    echo "==> Local .env file already exists."
fi

echo "==> Initialization complete! You can now start containers with:"
echo "    docker compose up --build"
