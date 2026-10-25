#!/bin/sh
# Safe tag creation: validates name and ensures branch is up to date.
set -e

TAG="$1"
if [ -z "$TAG" ]; then
    echo "Usage: bash ~/.git-hooks/tag-version.sh <tag>  (e.g. v1.2.3)"
    exit 1
fi

if ! echo "$TAG" | grep -Eq '^v[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?(-[A-Za-z0-9.-]+)?(\+[A-Za-z0-9.-]+)?$'; then
    echo "[ERROR] Tag must follow vMAJOR.MINOR.PATCH (e.g. v1.2.3)"
    exit 1
fi

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
[ -z "$REPO_ROOT" ] && { echo "[ERROR] Not a git repository"; exit 1; }
cd "$REPO_ROOT"

current_branch=$(git symbolic-ref --short HEAD 2>/dev/null)
if [ -n "$current_branch" ]; then
    upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null)
    if [ -n "$upstream" ]; then
        git fetch --quiet 2>/dev/null || true
        behind=$(git rev-list --count HEAD.."$upstream" 2>/dev/null)
        if [ "${behind:-0}" -gt 0 ]; then
            echo "[ERROR] Branch '$current_branch' is behind '$upstream' by ${behind} commit(s). Pull before tagging."
            exit 1
        fi
    fi
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "[ERROR] Tag '$TAG' already exists"
    exit 1
fi

git tag "$TAG"
echo "[OK] Created tag $TAG"
