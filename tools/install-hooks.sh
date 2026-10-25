#!/bin/sh
cd "$(dirname "$0")/.." || exit 1
git config core.hooksPath .githooks
git config alias.tag-version '!bash .githooks/tag-version.sh'
echo "Hooks installed. core.hooksPath=.githooks"
