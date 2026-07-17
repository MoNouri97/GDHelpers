#!/bin/bash
set -e

if [ -z "$1" ]; then
  echo "Usage: ./release.sh <version>"
  echo "  e.g. ./release.sh 1.0.5"
  exit 1
fi

VERSION="$1"

git tag "v$VERSION"
git push origin master --tags

echo "Tagged v$VERSION — workflow will publish to NuGet."
