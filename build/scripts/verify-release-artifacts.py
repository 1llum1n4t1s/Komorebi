#!/usr/bin/env python3
"""配信する実物を照合する。アプリは起動せず、失敗時は upload を阻止する。"""

import argparse
import hashlib
import io
import json
import gzip
import lzma
from pathlib import Path
import plistlib
import re
import struct
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import zlib


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(*args):
    return subprocess.check_output(args)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def check_binary(data, rid, version, source_sha):
    if rid.startswith("win-"):
        require(data[:2] == b"MZ", f"{rid}: missing PE header")
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        require(data[pe:pe + 4] == b"PE\0\0", f"{rid}: invalid PE header")
        cpu = struct.unpack_from("<H", data, pe + 4)[0]
        require(cpu == {"win-x64": 0x8664, "win-arm64": 0xAA64}[rid], f"{rid}: wrong PE CPU")
        # VERSIONINFO の文字列値を実物から読む（AOT exe にも含まれる）。
        for key, expected in (("FileVersion", version + ".0"), ("ProductVersion", version)):
            marker = (key + "\0").encode("utf-16le")
            start = data.find(marker)
            require(start >= 0, f"{rid}: missing {key}")
            start = (start + len(marker) + 3) & ~3
            end = start
            while data[end:end + 2] != b"\0\0":
                require(end + 2 <= len(data), f"{rid}: truncated VERSIONINFO")
                end += 2
            value = data[start:end].decode("utf-16le")
            require(value.split("+")[0] == expected, f"{rid}: wrong {key}: {value}")
            if key == "ProductVersion" and "+" in value:
                require(value == version + "+" + source_sha, f"{rid}: wrong source SHA")
    elif rid.startswith("linux-"):
        require(data[:6] == b"\x7fELF\x02\x01", f"{rid}: expected ELF64 little endian")
        require(struct.unpack_from("<H", data, 18)[0] == (62 if rid.endswith("x64") else 183), f"{rid}: wrong ELF CPU")
    else:
        require(data[:4] == b"\xcf\xfa\xed\xfe", f"{rid}: expected Mach-O64")
        require(struct.unpack_from("<I", data, 4)[0] == 0x100000C, f"{rid}: wrong Mach-O CPU")
    # AOT 内に informational version が残る場合も、別コミットを混ぜない。
    for encoded in (data, data.decode("utf-16le", errors="ignore").encode("utf-8")):
        revisions = re.findall(re.escape(version.encode()) + rb"\+([0-9a-f]{40})(?![0-9a-f])", encoded)
        require(all(rev.decode() == source_sha for rev in revisions), f"{rid}: embedded source SHA differs")


def zip_app(path, rid, version, source_sha, package=False):
    name = "komorebi" if rid.startswith("linux-") else "Komorebi.exe" if rid.startswith("win-") else "Komorebi"
    with zipfile.ZipFile(path) as archive:
        require(archive.testzip() is None, f"{path.name}: corrupt ZIP")
        names = archive.namelist()
        require(len(names) == len(set(names)), f"{path.name}: duplicate ZIP entries")
        if package and rid.startswith("linux-"):
            apps = [entry for entry in names if entry == "lib/app/Komorebi.AppImage"]
        elif rid.startswith("osx-"):
            apps = [entry for entry in names if entry.endswith("Contents/MacOS/Komorebi")]
        else:
            apps = [entry for entry in names if entry.split("/")[-1] == name]
        require(len(apps) == 1, f"{path.name}: expected exactly one {name}")
        if package:
            specs = [entry for entry in names if entry.endswith(".nuspec")]
            require(len(specs) == 1, f"{path.name}: missing/duplicate nuspec")
            root = ET.fromstring(archive.read(specs[0]))
            metadata = next(node for node in root if node.tag.split("}")[-1] == "metadata")
            fields = {node.tag.split("}")[-1]: node.text for node in metadata}
            # macOS の vpk は nuspec rid に 'osx' を記録する。CPU は Mach-O で照合する。
            spec_rid = "osx" if rid.startswith("osx-") else rid
            for key, expected in {"id": "Komorebi", "version": version, "rid": spec_rid, "channel": rid, "mainExe": name}.items():
                require(fields.get(key) == expected, f"{path.name}: wrong nuspec {key}: {fields.get(key)}")
            for node in metadata:
                if node.tag.split("}")[-1] == "repository" and node.get("commit"):
                    require(node.get("commit") == source_sha, f"{path.name}: wrong nuspec source SHA")
        if rid.startswith("osx-"):
            # 主実行ファイルに対応する plist のみ照合し、同梱 dSYM の metadata は含めない。
            plist = apps[0].removesuffix("MacOS/Komorebi") + "Info.plist"
            require(names.count(plist) == 1, f"{path.name}: missing/duplicate main app Info.plist")
            info = plistlib.loads(archive.read(plist))
            require(info.get("CFBundleShortVersionString") == version and info.get("CFBundleVersion") == version + ".0", f"{path.name}: wrong bundle version")
        data = archive.read(apps[0])
    if package and rid.startswith("linux-"):
        with tempfile.TemporaryDirectory(prefix="komorebi-release-") as work:
            image = Path(work) / "Komorebi.AppImage"
            image.write_bytes(data)
            data = appimage_app(image, velopack=True)
    check_binary(data, rid, version, source_sha)
    return digest(data)


def tar_app(data):
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        apps = [entry for entry in archive.getmembers() if entry.name.lstrip("./") == "opt/komorebi/komorebi"]
        require(len(apps) == 1, "DEB must contain opt/komorebi/komorebi")
        return archive.extractfile(apps[0]).read()


def rpm_app(path):
    data = run("rpm2cpio", str(path))
    offset = 0
    apps = []
    while offset < len(data):
        header = data[offset:offset + 110]
        require(header[:6] in (b"070701", b"070702"), "invalid RPM cpio header")
        size = int(header[54:62], 16)
        name_size = int(header[94:102], 16)
        name = data[offset + 110:offset + 110 + name_size - 1].decode()
        offset = (offset + 110 + name_size + 3) & ~3
        if name.lstrip("./") == "opt/komorebi/komorebi":
            apps.append(data[offset:offset + size])
        offset = (offset + size + 3) & ~3
        if name == "TRAILER!!!":
            break
    require(len(apps) == 1, f"{path.name}: missing/duplicate RPM app")
    return apps[0]


def appimage_app(path, velopack=False):
    data = path.read_bytes()
    require(data[8:11] == b"AI\x02", f"{path.name}: invalid AppImage type")
    offsets = [match.start() for match in re.finditer(b"hsqs", data)]
    # squashfs superblock の version 4 を確認し、実行せず payload を読む。
    offsets = [offset for offset in offsets if data[offset + 28:offset + 32] == b"\x04\0\0\0"]
    require(len(offsets) == 1, f"{path.name}: missing/ambiguous squashfs")
    app = "usr/bin/komorebi" if velopack else "opt/komorebi/komorebi"
    return run("unsquashfs", "-no-progress", "-o", str(offsets[0]), "-cat", str(path), app)


def pkg_app(path, version):
    data = path.read_bytes()
    require(data[:4] == b"xar!", f"{path.name}: invalid XAR")
    header_size, _, toc_size = struct.unpack_from(">HHQ", data, 4)
    toc = ET.fromstring(zlib.decompress(data[header_size:header_size + toc_size]))
    heap = header_size + toc_size
    contents = {}
    for entry in toc.iter("file"):
        name = entry.findtext("name")
        if name not in ("PackageInfo", "Payload"):
            continue
        require(name not in contents, f"{path.name}: duplicate {name}")
        info = entry.find("data")
        start = heap + int(info.findtext("offset"))
        value = data[start:start + int(info.findtext("length"))]
        archived = info.find("archived-checksum")
        require(hashlib.new(archived.get("style"), value).hexdigest() == archived.text, f"{path.name}: XAR checksum mismatch")
        if info.find("encoding").get("style") == "application/x-gzip":
            value = zlib.decompress(value)
        require(len(value) == int(info.findtext("size")), f"{path.name}: wrong XAR entry size")
        contents[name] = value
    info = ET.fromstring(contents["PackageInfo"])
    require(info.get("version") == version, f"{path.name}: wrong installer version")
    payload = contents["Payload"]
    if payload.startswith(b"pbzx"):
        offset = 12
        chunks = []
        while offset < len(payload):
            flags, size = struct.unpack_from(">QQ", payload, offset)
            offset += 16
            chunk = payload[offset:offset + size]
            chunks.append(lzma.decompress(chunk) if chunk.startswith(b"\xfd7zXZ\0") else chunk)
            offset += size
            if not flags & (1 << 24):
                break
        payload = b"".join(chunks)
    elif payload.startswith(b"\x1f\x8b"):
        payload = gzip.decompress(payload)
    names = subprocess.check_output(("bsdtar", "-tf", "-"), input=payload).decode().splitlines()
    apps = [name for name in names if name.endswith("Contents/MacOS/Komorebi")]
    require(len(apps) == 1, f"{path.name}: missing/duplicate installer app")
    return subprocess.check_output(("bsdtar", "-xOf", "-", apps[0]), input=payload)


def verify(root, version, source_sha):
    referenced = set()
    hashes = {}
    channels = ("osx-arm64", "linux-x64", "linux-arm64")
    for rid in channels:
        path = root / f"releases.{rid}.json"
        manifest = json.loads(path.read_text(encoding="utf-8-sig"))
        assets = manifest["Assets"]
        require(len(assets) == 1, f"{path.name}: expected one fresh full package")
        asset = assets[0]
        filename = f"Komorebi-{version}-{rid}-full.nupkg"
        require(asset["PackageId"] == "Komorebi" and asset["Version"] == version and asset["Type"] == "Full" and asset["FileName"] == filename, f"{path.name}: wrong identity")
        package = root / filename
        data = package.read_bytes()
        require(len(data) == asset["Size"] and hashlib.sha1(data).hexdigest().upper() == asset["SHA1"].upper() and digest(data).upper() == asset["SHA256"].upper(), f"{filename}: manifest size/hash mismatch")
        hashes[rid] = zip_app(package, rid, version, source_sha, package=True)
        referenced.add(filename)
    require({path.name for path in root.glob("*.nupkg")} == referenced, "unexpected/unreferenced nupkg")
    require({path.name for path in root.glob("releases.*.json")} == {f"releases.{rid}.json" for rid in channels}, "unexpected release channel")
    expected = {
        f"komorebi_{version}.windows-x64.zip": "win-x64",
        f"komorebi_{version}.windows-arm64.zip": "win-arm64",
        f"komorebi_{version}.macos-apple-silicon.zip": "osx-arm64",
        "Komorebi-osx-arm64-Portable.zip": "osx-arm64",
    }
    for filename, rid in expected.items():
        actual = zip_app(root / filename, rid, version, source_sha)
        if rid in hashes:
            require(actual == hashes[rid], f"{filename}: payload differs from nupkg")
    installer = root / "Komorebi-osx-arm64-Setup.pkg"
    data = pkg_app(installer, version)
    check_binary(data, "osx-arm64", version, source_sha)
    require(digest(data) == hashes["osx-arm64"], f"{installer.name}: payload differs from nupkg")
    expected[installer.name] = "osx-arm64"
    for rid, deb_arch, rpm_arch in (("linux-x64", "amd64", "x86_64"), ("linux-arm64", "arm64", "aarch64")):
        deb = root / f"komorebi_{version}-1_{deb_arch}.deb"
        metadata = run("dpkg-deb", "-f", str(deb), "Package", "Version", "Architecture").decode()
        require(metadata.splitlines() == ["Package: komorebi", f"Version: {version}-1", f"Architecture: {deb_arch}"], f"{deb.name}: wrong metadata")
        payloads = [(deb.name, tar_app(run("dpkg-deb", "--fsys-tarfile", str(deb))))]
        rpm = root / f"komorebi-{version}-1.{rpm_arch}.rpm"
        metadata = run("rpm", "-qp", "--qf", "%{NAME}|%{VERSION}|%{RELEASE}|%{ARCH}", str(rpm)).decode()
        require(metadata == f"komorebi|{version}|1|{rpm_arch}", f"{rpm.name}: wrong metadata")
        payloads.append((rpm.name, rpm_app(rpm)))
        for filename in (f"komorebi-{version}.linux.{deb_arch}.AppImage", f"Komorebi-{rid}.AppImage"):
            payloads.append((filename, appimage_app(root / filename, velopack=filename.startswith("Komorebi-"))))
        for filename, data in payloads:
            check_binary(data, rid, version, source_sha)
            require(digest(data) == hashes[rid], f"{filename}: payload differs from nupkg")
        expected.update({name: rid for name, _ in payloads})
    # 配信ディレクトリへ別版・未検証の archive が紛れた場合も停止する。
    known = set(expected) | referenced | {f"releases.{rid}.json" for rid in channels}
    for rid in channels:
        path = root / f"assets.{rid}.json"
        assets = json.loads(path.read_text(encoding="utf-8-sig"))
        required = {f"Komorebi-{version}-{rid}-full.nupkg", f"Komorebi-{rid}.AppImage"} if rid.startswith("linux-") else {f"Komorebi-{version}-{rid}-full.nupkg", f"Komorebi-{rid}-Setup.pkg", f"Komorebi-{rid}-Portable.zip"}
        require(len(assets) == len(required) and {entry["RelativeFileName"] for entry in assets} == required, f"{path.name}: wrong artifact inventory")
        known.add(path.name)
    for path in root.iterdir():
        require(path.is_file(), f"unexpected directory: {path}")
        if path.suffix.lower() in (".zip", ".appimage", ".deb", ".rpm", ".pkg"):
            require(path.name in expected, f"unexpected archive: {path.name}")
        if path.name.startswith("RELEASES"):
            lines = path.read_text(encoding="utf-8-sig").splitlines()
            require(lines, f"{path.name}: empty legacy manifest")
            for line in lines:
                sha1, filename, size = line.split()
                require(filename in referenced, f"{path.name}: unexpected package reference")
                data = (root / filename).read_bytes()
                require(hashlib.sha1(data).hexdigest().upper() == sha1.upper() and len(data) == int(size), f"{path.name}: wrong hash/size")
            known.add(path.name)
        require(path.name in known, f"unverified artifact: {path.name}")
    print(f"Verified {len(list(root.iterdir()))} final release files: {version}, source {source_sha}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()
    source_version = ET.parse("Directory.Build.props").getroot().findtext("PropertyGroup/Version")
    require(args.version == source_version, "workflow version differs from source")
    require(re.fullmatch(r"[0-9a-f]{40}", args.source_sha), "invalid source SHA")
    verify(args.packages, args.version, args.source_sha)
