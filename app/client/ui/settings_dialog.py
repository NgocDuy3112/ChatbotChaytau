from __future__ import annotations

from dataclasses import dataclass

from PyQt6.QtWidgets import (
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QDoubleSpinBox,
    QFormLayout,
    QTextEdit,
    QVBoxLayout,
)


@dataclass(slots=True)
class AppSettingsValues:
    base_url: str
    timeout: float
    default_model: str
    default_instructions: str


class SettingsDialog(QDialog):
    def __init__(
        self,
        *,
        current_values: AppSettingsValues,
        available_models: list[str],
        parent=None,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("Cài đặt")
        self.resize(500, 300)
        self._base_url = current_values.base_url

        layout = QVBoxLayout(self)
        form = QFormLayout()
        form.setSpacing(10)

        self.timeout_input = QDoubleSpinBox()
        self.timeout_input.setRange(5.0, 300.0)
        self.timeout_input.setDecimals(1)
        self.timeout_input.setSingleStep(5.0)
        self.timeout_input.setSuffix(" giây")
        self.timeout_input.setValue(current_values.timeout)
        form.addRow("Request timeout", self.timeout_input)

        self.model_input = QComboBox()
        self.model_input.addItems(available_models)
        if current_values.default_model and self.model_input.findText(current_values.default_model) < 0:
            self.model_input.addItem(current_values.default_model)
        if current_values.default_model:
            self.model_input.setCurrentText(current_values.default_model)
        form.addRow("Model mặc định", self.model_input)

        self.instructions_input = QTextEdit()
        self.instructions_input.setPlaceholderText("Chỉ dẫn hệ thống mặc định (không bắt buộc)")
        self.instructions_input.setPlainText(current_values.default_instructions)
        self.instructions_input.setFixedHeight(120)
        form.addRow("System instructions", self.instructions_input)

        layout.addLayout(form)

        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel
        )
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout.addWidget(buttons)

    def values(self) -> AppSettingsValues:
        return AppSettingsValues(
            base_url=self._base_url,
            timeout=float(self.timeout_input.value()),
            default_model=self.model_input.currentText().strip(),
            default_instructions=self.instructions_input.toPlainText().strip(),
        )
