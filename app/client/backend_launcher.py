from __future__ import annotations

from dataclasses import dataclass
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path
from urllib.parse import urlparse

import httpx


@dataclass(slots=True)
class BackendLaunchState:
    base_url: str
    process: subprocess.Popen[str] | None = None
    started_by_client: bool = False
    error: str | None = None

    def stop(self) -> None:
        if not self.process:
            return
        if self.process.poll() is not None:
            return

        self.process.terminate()
        try:
            self.process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.process.kill()


def ensure_backend_running(default_url: str = "http://127.0.0.1:8000") -> BackendLaunchState:
    base_url = (os.getenv("APP_BACKEND_URL") or default_url).rstrip("/")
    state = BackendLaunchState(base_url=base_url)

    if not _env_bool("APP_AUTO_START_BACKEND", default=True):
        return state

    if _backend_is_alive(base_url):
        return state

    parsed = urlparse(base_url)
    host = parsed.hostname or "127.0.0.1"
    port = parsed.port or (443 if parsed.scheme == "https" else 8000)

    if not _is_local_host(host):
        state.error = (
            "Auto-start only supports local backend URLs. "
            f"Current APP_BACKEND_URL={base_url}"
        )
        return state

    server_dir = _resolve_server_dir()
    if not server_dir.exists():
        state.error = (
            f"Server directory not found: {server_dir}. "
            "Set APP_SERVER_DIR to a folder containing main.py for backend."
        )
        return state

    python_exec = _resolve_python_executable(server_dir)
    if not python_exec:
        state.error = (
            "Could not resolve a Python interpreter for backend startup. "
            "Set APP_SERVER_PYTHON to your python.exe path."
        )
        return state

    app_import, app_dir = _resolve_uvicorn_app(server_dir)

    command = [
        python_exec,
        "-m",
        "uvicorn",
        app_import,
        "--host",
        host,
        "--port",
        str(port),
    ]
    if app_dir:
        command.extend(["--app-dir", str(app_dir)])

    backend_log_path = _resolve_backend_log_path(server_dir)
    stdout_target: int | object = subprocess.DEVNULL
    stderr_target: int | object = subprocess.DEVNULL
    backend_log_handle = None
    if backend_log_path:
        backend_log_path.parent.mkdir(parents=True, exist_ok=True)
        backend_log_handle = backend_log_path.open("a", encoding="utf-8")
        backend_log_handle.write(
            "\n=== backend auto-start at "
            f"{time.strftime('%Y-%m-%d %H:%M:%S')} | "
            f"cmd={' '.join(command)} | cwd={str(app_dir or server_dir)} ===\n"
        )
        backend_log_handle.flush()
        stdout_target = backend_log_handle
        stderr_target = subprocess.STDOUT

    creation_flags = 0
    if os.name == "nt":
        creation_flags = getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)

    try:
        process = subprocess.Popen(
            command,
            cwd=str(app_dir or server_dir),
            env=os.environ.copy(),
            creationflags=creation_flags,
            stdout=stdout_target,
            stderr=stderr_target,
        )
    except Exception as exc:
        if backend_log_handle:
            backend_log_handle.close()
        state.error = f"Failed to start backend server: {exc}"
        return state
    finally:
        if backend_log_handle:
            backend_log_handle.close()

    if _wait_until_ready(base_url, timeout_seconds=12, process=process):
        state.process = process
        state.started_by_client = True
        return state

    process_exit_code = process.poll()

    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.kill()

    log_tail = _read_backend_log_tail(backend_log_path)
    detail = f"\nBackend log tail:\n{log_tail}" if log_tail else ""

    state.error = (
        "Backend did not become ready in time. "
        f"server_dir={server_dir}, python={python_exec}, exit_code={process_exit_code}. "
        "Set APP_SERVER_DIR and APP_SERVER_PYTHON explicitly if needed."
        f"{detail}"
    )
    return state


def _backend_is_alive(base_url: str) -> bool:
    try:
        with httpx.Client(timeout=1.5) as client:
            response = client.get(f"{base_url}/")
            return response.status_code < 500
    except Exception:
        return False


def _wait_until_ready(
    base_url: str,
    timeout_seconds: float,
    process: subprocess.Popen[str] | None = None,
) -> bool:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if _backend_is_alive(base_url):
            return True
        if process and process.poll() is not None:
            return False
        time.sleep(0.3)
    return False


def _env_bool(name: str, default: bool) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default
    return raw.strip().lower() in {"1", "true", "yes", "on"}


def _is_local_host(host: str) -> bool:
    return host in {"localhost", "127.0.0.1", "0.0.0.0", "::1"}


def _resolve_server_dir() -> Path:
    configured = os.getenv("APP_SERVER_DIR")
    if configured:
        return Path(configured).expanduser().resolve()

    module_relative = Path(__file__).resolve().parents[1] / "server"
    if module_relative.exists():
        return module_relative

    exe_parent = Path(sys.executable).resolve().parent
    exe_candidates = [
        exe_parent / "app" / "server",
        exe_parent.parent / "app" / "server",
        exe_parent.parent.parent / "app" / "server",
        exe_parent / "server",
    ]
    for candidate in exe_candidates:
        if candidate.exists():
            return candidate.resolve()

    cwd = Path.cwd()
    candidates = [
        cwd / "app" / "server",
        cwd / "server",
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate.resolve()

    return module_relative


def _resolve_python_executable(server_dir: Path) -> str | None:
    configured = os.getenv("APP_SERVER_PYTHON")
    if configured:
        path = Path(configured).expanduser().resolve()
        if path.exists():
            return str(path)

    if not getattr(sys, "frozen", False):
        return sys.executable

    candidates = [
        server_dir.parent / ".venv" / "Scripts" / "python.exe",
        server_dir.parent / ".venv" / "bin" / "python",
        Path.cwd() / "app" / ".venv" / "Scripts" / "python.exe",
        Path.cwd() / ".venv" / "Scripts" / "python.exe",
    ]
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved.exists():
            return str(resolved)

    python_from_path = shutil.which("python")
    if python_from_path:
        return python_from_path

    py_launcher = shutil.which("py")
    if py_launcher:
        return py_launcher

    return None


def _resolve_uvicorn_app(server_dir: Path) -> tuple[str, Path | None]:
    """Resolve the uvicorn app target and optional --app-dir.

    For this project we prefer package import `server.main:app` so relative
    imports in `server/main.py` work correctly.
    """
    if server_dir.name == "server" and (server_dir / "__init__.py").exists():
        return ("server.main:app", server_dir.parent)
    return ("main:app", None)


def _resolve_backend_log_path(server_dir: Path) -> Path | None:
    configured = os.getenv("APP_BACKEND_LOG_FILE")
    if configured:
        return Path(configured).expanduser().resolve()

    repo_root = server_dir.parent.parent if server_dir.name == "server" else Path.cwd()
    return (repo_root / "logs" / "backend_autostart.log").resolve()


def _read_backend_log_tail(log_path: Path | None, max_lines: int = 25, max_chars: int = 4000) -> str:
    if not log_path or not log_path.exists():
        return ""

    try:
        lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
    except Exception:
        return ""

    tail = "\n".join(lines[-max_lines:]).strip()
    if len(tail) > max_chars:
        tail = tail[-max_chars:]
    return tail
