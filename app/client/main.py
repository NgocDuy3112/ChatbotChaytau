try:
    from .app import run
except ImportError:
    from app.client.app import run


if __name__ == "__main__":
    raise SystemExit(run())
