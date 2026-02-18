from __future__ import annotations

import sys

from PyQt6.QtWidgets import QApplication, QMessageBox

from .backend_launcher import ensure_backend_running
from .ui.main_window import MainWindow


def run() -> int:
    app = QApplication(sys.argv)
    app.setApplicationName("Chatbot Desktop")

    launch_state = ensure_backend_running()

    window = MainWindow(base_url=launch_state.base_url)

    if launch_state.started_by_client:
        window.statusBar().showMessage("Backend nội bộ đã được khởi động tự động", 5000)
    if launch_state.error:
        QMessageBox.warning(
            window,
            "Khởi động backend",
            f"Không thể tự khởi động backend.\n{launch_state.error}",
        )

    window.show()
    exit_code = app.exec()
    launch_state.stop()
    return exit_code
