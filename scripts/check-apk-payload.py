#!/usr/bin/env python3
"""Reject fast-deployment APKs that cannot launch after a standalone install."""
import argparse
from pathlib import Path
import zipfile


APP_ASSEMBLIES = ("MuscleForge.Mobile", "MuscleForge.Mobile.Core", "MuscleForge.Client")


def check_apk(path):
    with zipfile.ZipFile(path) as apk:
        names = set(apk.namelist())
        for required in ("AndroidManifest.xml", "classes.dex"):
            if required not in names:
                raise ValueError(f"{path}: missing {required}")

        abis = sorted({name.split("/")[1] for name in names
                       if name.startswith("lib/") and name.endswith("/libmonodroid.so")})
        if not abis:
            raise ValueError(f"{path}: missing the .NET Android runtime")

        for abi in abis:
            store = f"lib/{abi}/libassembly-store.so"
            payloads = [store] if store in names else [
                f"lib/{abi}/lib_{assembly}.dll.so" for assembly in APP_ASSEMBLIES
            ]
            for payload in payloads:
                if payload not in names:
                    raise ValueError(
                        f"{path}: missing managed app code for {abi}: {payload}. "
                        "Build with EmbedAssembliesIntoApk=true; fast-deployment APKs "
                        "cannot launch after a standalone install."
                    )
                with apk.open(payload) as entry:
                    if entry.read(4) != b"\x7fELF":
                        raise ValueError(f"{path}: invalid managed payload: {payload}")

    print(f"{path}: standalone managed payload verified for {', '.join(abis)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path, nargs="+")
    args = parser.parse_args()
    try:
        for path in args.apk:
            check_apk(path)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error)) from error


if __name__ == "__main__":
    main()
