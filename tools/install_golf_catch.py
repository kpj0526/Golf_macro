#!/usr/bin/env python3
"""Download and install the latest public Golf Catch Windows release.

This script intentionally downloads only a published GitHub Release asset. It never
reads, uploads, or creates account credentials.
"""

from __future__ import annotations

import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile


# Public repository that hosts the packaged customer release assets.
REPOSITORY = "kpj0526/Golf_macro"
ASSET_NAME = "GolfCatch-win-x64.zip"
API_URL = f"https://api.github.com/repos/{REPOSITORY}/releases/latest"


def latest_asset_url() -> str:
    request = urllib.request.Request(API_URL, headers={"Accept": "application/vnd.github+json"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            release = json.load(response)
    except Exception as exc:  # pragma: no cover - network dependent
        raise SystemExit(f"GitHub 최신 릴리스를 확인할 수 없습니다: {exc}") from exc

    for asset in release.get("assets", []):
        if asset.get("name") == ASSET_NAME:
            return asset["browser_download_url"]
    raise SystemExit(
        f"최신 GitHub 릴리스에 {ASSET_NAME} 파일이 없습니다. "
        "릴리스 자산을 게시한 뒤 다시 실행하세요."
    )


def safe_extract(archive: Path, destination: Path) -> None:
    with zipfile.ZipFile(archive) as bundle:
        root = destination.resolve()
        for entry in bundle.infolist():
            target = (destination / entry.filename).resolve()
            if target != root and root not in target.parents:
                raise SystemExit("안전하지 않은 ZIP 경로가 감지되어 설치를 중단했습니다.")
        bundle.extractall(destination)


def main() -> None:
    parser = argparse.ArgumentParser(description="Golf Catch 최신 릴리스 설치")
    # Keep the executable separate from %LOCALAPPDATA%\GolfCatch\pw.dat,
    # the application's DPAPI-protected credential store.
    default_dir = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "GolfCatch" / "app"
    parser.add_argument("--install-dir", type=Path, default=default_dir, help="설치 폴더")
    args = parser.parse_args()

    destination = args.install_dir.expanduser().resolve()
    url = latest_asset_url()
    print("최신 Golf Catch 릴리스를 내려받는 중입니다…")

    with tempfile.TemporaryDirectory(prefix="golf-catch-install-") as temp_dir:
        archive = Path(temp_dir) / ASSET_NAME
        try:
            urllib.request.urlretrieve(url, archive)  # nosec B310: URL is GitHub release API output
        except Exception as exc:  # pragma: no cover - network dependent
            raise SystemExit(f"다운로드에 실패했습니다: {exc}") from exc

        staging = Path(temp_dir) / "staging"
        staging.mkdir()
        safe_extract(archive, staging)

        backup = None
        if destination.exists():
            # Keep the prior install recoverable. Credentials live in a separate
            # DPAPI store and are not part of either directory.
            backup = destination.with_name(
                f"{destination.name}.backup-{datetime.now():%Y%m%d%H%M%S}"
            )
            destination.rename(backup)
        try:
            shutil.move(str(staging), str(destination))
        except Exception:
            if backup is not None and backup.exists() and not destination.exists():
                backup.rename(destination)
            raise

    executable = destination / "booking.exe"
    if not executable.is_file():
        raise SystemExit("설치 파일에 booking.exe가 없습니다. 릴리스 ZIP 구성을 확인하세요.")
    print(f"설치 완료: {destination}")
    print(f"실행 파일: {executable}")


if __name__ == "__main__":
    main()
