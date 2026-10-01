"""Package completed Unity WebGL and Linux builds without editor/debug output."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile
import time
import zipfile


def shipping(path):
    return not any("DoNotShip" in part or "DontShip" in part or part.lower() == "logs"
                   for part in path.parts)


def tar_metadata(info):
    if not shipping(Path(info.name)):
        return None
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    info.mode = 0o755 if info.isdir() or info.name.endswith((".x86_64", ".so")) else 0o644
    return info


def zip_add(archive, name, data):
    # Web servers read the unpacked files as another user; ZipFile.writestr would default to 0600.
    info = zipfile.ZipInfo(name, date_time=time.localtime()[:6])
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = (0o100644 << 16)
    archive.writestr(info, data)


def package_webgl(web, destination):
    for required in (web / "index.html", web / "Configs/Network/client-network.json"):
        if not required.is_file():
            raise FileNotFoundError(required)
    for extension in ("*.wasm", "*.data", "*.loader.js", "*.framework.js"):
        if not list((web / "Build").glob(extension)):
            raise FileNotFoundError(f"WebGL/Build/{extension}")
    files = [path for path in sorted(web.rglob("*"))
             if path.is_file() and shipping(path.relative_to(web))]
    build_hashes = {path.relative_to(web / "Build").as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                    for path in files if path.is_relative_to(web / "Build")}
    version = hashlib.sha256(json.dumps(build_hashes, sort_keys=True).encode()).hexdigest()[:16]
    build_url = f"Build/{version}"
    index, replacements = re.subn(r'var buildUrl = "Build";', f'var buildUrl = "{build_url}";',
                                  (web / "index.html").read_text(encoding="utf-8"))
    if replacements != 1:
        raise ValueError("Expected one Aervento template buildUrl assignment; refusing an unversioned release")
    temporary = destination.with_suffix(destination.suffix + ".tmp")
    with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            relative = path.relative_to(web).as_posix()
            if relative == "index.html":
                zip_add(archive, relative, index)
            elif relative.startswith("Build/"):
                zip_add(archive, build_url + relative[len("Build"):], path.read_bytes())
            else:
                zip_add(archive, relative, path.read_bytes())
        zip_add(archive, "WEBGL-MANIFEST.json", json.dumps({
            "buildVersion": version, "buildUrl": build_url, "sha256": build_hashes
        }, indent=2) + "\n")
    temporary.replace(destination)
    print(f"WebGL resources: {build_url}")


def package_linux(linux, destination):
    executables = list(linux.glob("*.x86_64"))
    if len(executables) != 1:
        raise ValueError(f"Expected one Linux executable, found {len(executables)}")
    executable = executables[0]
    data_directory = linux / f"{executable.stem}_Data"
    if not data_directory.is_dir():
        raise FileNotFoundError(data_directory)
    # The deployment name is stable even if Unity's build output was named differently.
    linux_names = {executable.name: "PokerServer.x86_64", data_directory.name: "PokerServer_Data"}
    for source, deployed_name in linux_names.items():
        if source != deployed_name and (linux / deployed_name).exists():
            raise ValueError(f"Conflicting Linux deployment path: {deployed_name}")
    for required in (linux / "UnityPlayer.so", linux / "Configs/Network/server-network.json"):
        if not required.is_file():
            raise FileNotFoundError(required)
    temporary = destination.with_suffix(destination.suffix + ".tmp")
    with tarfile.open(temporary, "w:gz", compresslevel=6) as archive:
        for path in sorted(linux.iterdir()):
            archive.add(path, arcname=linux_names.get(path.name, path.name), filter=tar_metadata)
    temporary.replace(destination)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path, help="Directory containing WebGL/ and Linux/")
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument("--linux-only", action="store_true", help="Keep the existing WebGL archive unchanged")
    selection.add_argument("--webgl-only", action="store_true", help="Keep the existing Linux archive unchanged")
    args = parser.parse_args()
    root = args.directory.resolve()
    archives = [root / "Poker-WebGL.zip", root / "Poker-Linux-Server.tar.gz"]
    if args.linux_only and not archives[0].is_file():
        raise FileNotFoundError(archives[0])
    if args.webgl_only and not archives[1].is_file():
        raise FileNotFoundError(archives[1])
    if not args.linux_only:
        package_webgl(root / "WebGL", archives[0])
    if not args.webgl_only:
        package_linux(root / "Linux", archives[1])
    manifest = {}
    for path in archives:
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
        manifest[path.name] = {"bytes": path.stat().st_size, "sha256": digest.hexdigest()}
    (root / "SHA256.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
