#!/usr/bin/env bash

set -euo pipefail

cd build

mkdir -p Komorebi.app/Contents/Resources
mv Komorebi Komorebi.app/Contents/MacOS
cp resources/app/App.icns Komorebi.app/Contents/Resources/App.icns
sed "s/KOMOREBI_VERSION/$VERSION/g" resources/app/App.plist > Komorebi.app/Contents/Info.plist
rm -rf Komorebi.app/Contents/MacOS/Komorebi.dsym
rm -f Komorebi.app/Contents/MacOS/*.pdb

zip "komorebi_$VERSION.$RUNTIME.zip" -r Komorebi.app
