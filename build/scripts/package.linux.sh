#!/usr/bin/env bash

set -euo pipefail

# ICU versions to support (Debian has no virtual package, must list all)
# Format: space-separated version numbers
ICU_VERSIONS="78 77 76 74 72 71 70 69 68 67 66 65 63"

arch=
appimage_arch=
target=
case "$RUNTIME" in
    linux-x64)
        arch=amd64
        appimage_arch=x86_64
        target=x86_64;;
    linux-arm64)
        arch=arm64
        appimage_arch=arm_aarch64
        target=aarch64;;
    *)
        echo "Unknown runtime $RUNTIME"
        exit 1;;
esac

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage

cd build

if [[ ! -f "appimagetool" ]]; then
    curl -o appimagetool -L "$APPIMAGETOOL_URL"
    chmod +x appimagetool
fi

rm -f Komorebi/*.dbg
rm -f Komorebi/*.pdb

mkdir -p Komorebi.AppDir/opt
mkdir -p Komorebi.AppDir/usr/share/metainfo
mkdir -p Komorebi.AppDir/usr/share/applications

cp -r Komorebi Komorebi.AppDir/opt/komorebi
desktop-file-install resources/_common/applications/komorebi.desktop --dir Komorebi.AppDir/usr/share/applications \
    --set-icon com.1llum1n4t1s.Komorebi --set-key=Exec --set-value=AppRun
mv Komorebi.AppDir/usr/share/applications/{komorebi,com.1llum1n4t1s.Komorebi}.desktop
cp resources/_common/icons/komorebi.png Komorebi.AppDir/com.1llum1n4t1s.Komorebi.png
ln -rsf Komorebi.AppDir/opt/komorebi/komorebi Komorebi.AppDir/AppRun
ln -rsf Komorebi.AppDir/usr/share/applications/com.1llum1n4t1s.Komorebi.desktop Komorebi.AppDir
cp resources/appimage/komorebi.appdata.xml Komorebi.AppDir/usr/share/metainfo/com.1llum1n4t1s.Komorebi.appdata.xml

ARCH="$appimage_arch" ./appimagetool -v Komorebi.AppDir "komorebi-$VERSION.linux.$arch.AppImage"

mkdir -p resources/deb/opt/komorebi/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -f Komorebi/* resources/deb/opt/komorebi
ln -rsf resources/deb/opt/komorebi/komorebi resources/deb/usr/bin
cp -r resources/_common/applications resources/deb/usr/share
cp -r resources/_common/icons resources/deb/usr/share

# Calculate installed size in KB
installed_size=$(du -sk resources/deb | cut -f1)

# Generate ICU dependencies string for Debian
# Debian lacks libicu virtual package, must list all versions with OR operator
icu_deps="libicu"
for v in $ICU_VERSIONS; do
    icu_deps="$icu_deps | libicu$v"
done

# Update the control file (replace placeholder, not whole Depends line)
sed -i -e "s/^Version:.*/Version: $VERSION/" \
    -e "s/^Architecture:.*/Architecture: $arch/" \
    -e "s/^Installed-Size:.*/Installed-Size: $installed_size/" \
    -e "s/@ICU_DEPS@/$icu_deps/" \
    resources/deb/DEBIAN/control
# Ensure maintainer scripts are executable (required by dpkg-deb).
# upstream a6bbcab1 で postinst も追加されたため chmod 対象に含める。
chmod 0755 resources/deb/DEBIAN/preinst resources/deb/DEBIAN/postinst resources/deb/DEBIAN/prerm

# Build deb package with gzip compression
dpkg-deb -Zgzip --root-owner-group --build resources/deb "komorebi_$VERSION-1_$arch.deb"

rpmbuild -bb --target="$target" resources/rpm/SPECS/build.spec --define "_topdir $(pwd)/resources/rpm" --define "_version $VERSION" --define "__strip /bin/true"
mv "resources/rpm/RPMS/$target/komorebi-$VERSION-1.$target.rpm" ./
