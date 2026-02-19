from __future__ import annotations

import html
import re
import uuid
from datetime import datetime
from pathlib import Path

from PyQt6.QtCore import QEvent, QObject, QSettings, Qt, QUrl
from PyQt6.QtGui import QTextDocument
from PyQt6.QtPrintSupport import QPrinter
from PyQt6.QtWidgets import (
    QComboBox,
    QFileDialog,
    QHBoxLayout,
    QInputDialog,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMainWindow,
    QMessageBox,
    QMenu,
    QPushButton,
    QSplitter,
    QStatusBar,
    QTextBrowser,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from ..api.client import ApiError, ChatApiClient
from ..models.dto import BaseMessage, ChatRequest, Conversation
from ..state.store import ChatMessage, ChatState
from ..workers.stream_worker import ChatStreamWorker, StreamResult
from .settings_dialog import AppSettingsValues, SettingsDialog


class MainWindow(QMainWindow):
    def __init__(self, base_url: str | None = None):
        super().__init__()
        self.setWindowTitle("Chatbot Desktop")
        self.resize(1180, 760)

        self.client = ChatApiClient(base_url=base_url or "http://localhost:8000")
        self.state = ChatState()
        self.stream_worker: ChatStreamWorker | None = None
        self.settings = QSettings("ChatbotChaytau", "ChatbotDesktop")
        self.available_models = [
            "gemini-2.5-flash",
            "gemini-2.5-pro",
            "gemini-3-flash-preview",
            "gemini-3-pro-preview",
        ]

        self._build_ui()
        self._apply_styles()
        self._load_settings()
        self._load_conversations()

    def _build_ui(self) -> None:
        root = QWidget(self)
        root_layout = QVBoxLayout(root)
        root_layout.setContentsMargins(14, 14, 14, 12)
        root_layout.setSpacing(10)

        top_bar = QHBoxLayout()
        top_bar.setSpacing(8)

        title_label = QLabel("Trợ lý trò chuyện")
        title_label.setObjectName("appTitle")
        top_bar.addWidget(title_label)
        top_bar.addStretch(1)

        refresh_btn = QPushButton("Làm mới")
        refresh_btn.setObjectName("refreshButton")
        refresh_btn.clicked.connect(self._load_conversations)
        top_bar.addWidget(refresh_btn)

        settings_btn = QPushButton("Cài đặt")
        settings_btn.setObjectName("secondaryButton")
        settings_btn.clicked.connect(self._open_settings_dialog)
        top_bar.addWidget(settings_btn)
        root_layout.addLayout(top_bar)

        splitter = QSplitter(Qt.Orientation.Horizontal)
        splitter.setHandleWidth(1)

        left_panel = QWidget()
        left_panel.setObjectName("leftPanel")
        left_layout = QVBoxLayout(left_panel)
        left_layout.setContentsMargins(8, 8, 8, 8)
        left_layout.setSpacing(8)

        section_label = QLabel("Cuộc trò chuyện")
        section_label.setObjectName("sectionLabel")
        left_layout.addWidget(section_label)

        new_chat_btn = QPushButton("+ Trò chuyện mới")
        new_chat_btn.setObjectName("newChatButton")
        new_chat_btn.clicked.connect(self._new_chat)
        left_layout.addWidget(new_chat_btn)

        self.conversation_list = QListWidget()
        self.conversation_list.setObjectName("conversationList")
        self.conversation_list.itemSelectionChanged.connect(self._on_conversation_selected)
        self.conversation_list.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.conversation_list.customContextMenuRequested.connect(self._on_conversation_context_menu)
        left_layout.addWidget(self.conversation_list)
        splitter.addWidget(left_panel)

        right_panel = QWidget()
        right_panel.setObjectName("rightPanel")
        right_layout = QVBoxLayout(right_panel)
        right_layout.setContentsMargins(8, 8, 8, 8)
        right_layout.setSpacing(8)

        controls_row = QHBoxLayout()
        controls_row.setSpacing(8)
        self.model_input = QComboBox()
        self.model_input.setObjectName("modelInput")
        self.model_input.addItems(self.available_models)
        model_label = QLabel("Mô hình")
        model_label.setObjectName("fieldLabel")
        controls_row.addWidget(model_label)
        controls_row.addWidget(self.model_input)

        self.instructions_input = QLineEdit()
        self.instructions_input.setObjectName("instructionsInput")
        self.instructions_input.setPlaceholderText("Chỉ dẫn hệ thống (không bắt buộc)")
        controls_row.addWidget(self.instructions_input, 1)
        right_layout.addLayout(controls_row)

        self.chat_view = QTextBrowser()
        self.chat_view.setObjectName("chatView")
        self.chat_view.setOpenLinks(False)
        self.chat_view.anchorClicked.connect(self._on_chat_link_clicked)
        right_layout.addWidget(self.chat_view, 1)

        attachments_row = QHBoxLayout()
        attachments_row.setSpacing(8)
        attach_btn = QPushButton("Đính kèm tệp")
        attach_btn.setObjectName("secondaryButton")
        attach_btn.clicked.connect(self._attach_files)
        attachments_row.addWidget(attach_btn)

        clear_attach_btn = QPushButton("Xóa tệp")
        clear_attach_btn.setObjectName("secondaryButton")
        clear_attach_btn.clicked.connect(self._clear_attachments)
        attachments_row.addWidget(clear_attach_btn)

        self.attachment_label = QLabel("Chưa có tệp đính kèm")
        self.attachment_label.setObjectName("attachmentLabel")
        attachments_row.addWidget(self.attachment_label, 1)
        right_layout.addLayout(attachments_row)

        input_row = QHBoxLayout()
        input_row.setSpacing(8)

        self.input_box = QTextEdit()
        self.input_box.setObjectName("inputBox")
        self.input_box.setPlaceholderText("Nhập tin nhắn...")
        self.input_box.setFixedHeight(60)
        self.input_box.installEventFilter(self)
        input_row.addWidget(self.input_box, 1)

        self.send_button = QPushButton("Gửi")
        self.send_button.setObjectName("sendButton")
        self.send_button.setFixedSize(74, 60)
        self.send_button.clicked.connect(self._send_message)
        input_row.addWidget(self.send_button)

        right_layout.addLayout(input_row)

        splitter.addWidget(right_panel)
        splitter.setSizes([300, 900])
        root_layout.addWidget(splitter, 1)

        self.setCentralWidget(root)
        self.setStatusBar(QStatusBar())
        self.statusBar().showMessage("Sẵn sàng")

    def eventFilter(self, watched: QObject, event: QEvent) -> bool:
        if watched is self.input_box and event.type() == QEvent.Type.KeyPress:
            key = getattr(event, "key", lambda: None)()
            if key in (Qt.Key.Key_Return, Qt.Key.Key_Enter):
                modifiers = getattr(event, "modifiers", lambda: Qt.KeyboardModifier.NoModifier)()
                if modifiers & Qt.KeyboardModifier.ShiftModifier:
                    return False

                if self.send_button.isEnabled():
                    self._send_message()
                return True

        return super().eventFilter(watched, event)

    def _apply_styles(self) -> None:
        self.setStyleSheet(
            """
            QWidget {
                font-size: 13px;
            }

            QMainWindow {
                background: #f4f5f7;
            }

            #appTitle {
                font-size: 16px;
                font-weight: 700;
                color: #1f2937;
            }

            #leftPanel, #rightPanel {
                background: #ffffff;
                border: 1px solid #e5e7eb;
                border-radius: 10px;
            }

            #sectionLabel {
                font-size: 12px;
                font-weight: 600;
                color: #6b7280;
                padding-left: 2px;
            }

            #fieldLabel {
                color: #4b5563;
                font-weight: 600;
            }

            QPushButton {
                border: 1px solid #d1d5db;
                border-radius: 8px;
                background: #ffffff;
                padding: 6px 12px;
            }

            QPushButton:hover {
                background: #f3f4f6;
            }

            QPushButton:disabled {
                color: #9ca3af;
                background: #f3f4f6;
            }

            #newChatButton, #sendButton {
                background: #2563eb;
                color: white;
                border: 1px solid #2563eb;
                font-weight: 600;
            }

            #newChatButton:hover, #sendButton:hover {
                background: #1d4ed8;
            }

            #conversationList {
                border: 1px solid #e5e7eb;
                border-radius: 8px;
                outline: 0;
                padding: 4px;
            }

            #conversationList::item {
                padding: 10px 8px;
                border-radius: 6px;
            }

            #conversationList::item:selected {
                background: #e5edff;
                color: #1e3a8a;
            }

            #chatView {
                border: 1px solid #e5e7eb;
                border-radius: 8px;
                background: #fafafa;
                padding: 8px;
            }

            #inputBox, #instructionsInput, #modelInput {
                border: 1px solid #d1d5db;
                border-radius: 8px;
                background: #ffffff;
                padding: 6px;
            }

            #attachmentLabel {
                color: #4b5563;
            }
            """
        )

    def _new_chat(self) -> None:
        self.state.reset_chat()
        self.conversation_list.clearSelection()
        self._render_messages()
        self._update_attachment_label()
        self.statusBar().showMessage("Đã tạo cuộc trò chuyện mới", 3000)

    def _open_settings_dialog(self) -> None:
        dialog = SettingsDialog(
            current_values=self._current_settings_values(),
            available_models=list(self.available_models),
            parent=self,
        )
        if dialog.exec() != dialog.DialogCode.Accepted:
            return

        values = dialog.values()
        if not values.base_url:
            self._show_error("Backend URL không được để trống.")
            return

        previous_base_url = self.client.base_url
        self._apply_settings_values(values)
        self._persist_settings(values)

        if self.client.base_url != previous_base_url:
            self.state.reset_chat()
            self._render_messages()
            self._update_attachment_label()
            self._load_conversations()

        self.statusBar().showMessage("Đã lưu cài đặt", 3000)

    def _current_settings_values(self) -> AppSettingsValues:
        return AppSettingsValues(
            base_url=self.client.base_url,
            timeout=float(self.client.timeout),
            default_model=self.model_input.currentText().strip(),
            default_instructions=self.instructions_input.text().strip(),
        )

    def _apply_settings_values(self, values: AppSettingsValues) -> None:
        self.client.set_base_url(values.base_url)
        self.client.timeout = float(values.timeout)

        if values.default_model and self.model_input.findText(values.default_model) < 0:
            self.model_input.addItem(values.default_model)
        if values.default_model:
            self.model_input.setCurrentText(values.default_model)

        self.instructions_input.setText(values.default_instructions)

    def _persist_settings(self, values: AppSettingsValues) -> None:
        self.settings.setValue("client/base_url", values.base_url)
        self.settings.setValue("client/timeout", float(values.timeout))
        self.settings.setValue("chat/default_model", values.default_model)
        self.settings.setValue("chat/default_instructions", values.default_instructions)
        self.settings.sync()

    def _load_settings(self) -> None:
        base_url_raw = self.settings.value("client/base_url", self.client.base_url)
        timeout_raw = self.settings.value("client/timeout", self.client.timeout)
        model_raw = self.settings.value("chat/default_model", self.model_input.currentText())
        instructions_raw = self.settings.value("chat/default_instructions", "")

        base_url = str(base_url_raw or "").strip() or self.client.base_url
        try:
            timeout_value = float(timeout_raw)
        except (TypeError, ValueError):
            timeout_value = float(self.client.timeout)

        model = str(model_raw or "").strip() or self.model_input.currentText().strip()
        instructions = str(instructions_raw or "")

        self._apply_settings_values(
            AppSettingsValues(
                base_url=base_url,
                timeout=timeout_value,
                default_model=model,
                default_instructions=instructions,
            )
        )

    def _load_conversations(self) -> None:
        selected_id = self.state.current_conversation_id
        try:
            conversations = self.client.list_conversations()
        except ApiError as exc:
            self._show_error(str(exc))
            return

        self.conversation_list.blockSignals(True)
        self.conversation_list.clear()
        for conversation in conversations:
            label = self._conversation_label(conversation)
            item = QListWidgetItem(label)
            item.setData(Qt.ItemDataRole.UserRole, conversation.id)
            item.setData(Qt.ItemDataRole.UserRole + 1, conversation.title or "")
            item.setToolTip(conversation.title or label)
            self.conversation_list.addItem(item)
            if selected_id and conversation.id == selected_id:
                item.setSelected(True)
        self.conversation_list.blockSignals(False)

    def _on_conversation_selected(self) -> None:
        current_item = self.conversation_list.currentItem()
        if current_item is None:
            return
        conversation_id = current_item.data(Qt.ItemDataRole.UserRole)
        if not isinstance(conversation_id, str):
            return
        self._load_history(conversation_id)

    def _on_conversation_context_menu(self, position) -> None:
        item = self.conversation_list.itemAt(position)
        if item is None:
            return

        self.conversation_list.setCurrentItem(item)

        conversation_id = item.data(Qt.ItemDataRole.UserRole)
        if not isinstance(conversation_id, str):
            return

        menu = QMenu(self)
        rename_action = menu.addAction("Đổi tên cuộc trò chuyện")
        delete_action = menu.addAction("Xóa cuộc trò chuyện")
        chosen_action = menu.exec(self.conversation_list.viewport().mapToGlobal(position))
        if chosen_action == rename_action:
            self._rename_conversation(conversation_id)
        if chosen_action == delete_action:
            self._delete_conversation(conversation_id)

    def _rename_conversation(self, conversation_id: str) -> None:
        current_item = self.conversation_list.currentItem()
        current_title = ""
        if current_item is not None:
            maybe_title = current_item.data(Qt.ItemDataRole.UserRole + 1)
            if isinstance(maybe_title, str):
                current_title = maybe_title

        new_title, ok = QInputDialog.getText(
            self,
            "Đổi tên cuộc trò chuyện",
            "Tên mới:",
            text=current_title,
        )
        if not ok:
            return

        cleaned_title = new_title.strip()
        if not cleaned_title:
            self._show_error("Tên cuộc trò chuyện không được để trống.")
            return

        try:
            self.client.rename_conversation(conversation_id, cleaned_title)
        except ApiError as exc:
            self._show_error(str(exc))
            return

        self._load_conversations()
        self.statusBar().showMessage("Đã đổi tên cuộc trò chuyện", 3000)

    def _delete_conversation(self, conversation_id: str) -> None:
        answer = QMessageBox.question(
            self,
            "Xóa cuộc trò chuyện",
            "Bạn có chắc muốn xóa cuộc trò chuyện này và toàn bộ tin nhắn?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No,
        )
        if answer != QMessageBox.StandardButton.Yes:
            return

        try:
            self.client.delete_conversation(conversation_id)
        except ApiError as exc:
            self._show_error(str(exc))
            return

        if self.state.current_conversation_id == conversation_id:
            self.state.reset_chat()
            self._render_messages()
            self._update_attachment_label()

        self._load_conversations()
        self.statusBar().showMessage("Đã xóa cuộc trò chuyện", 3000)

    def _load_history(self, conversation_id: str) -> None:
        previous_conversation_id = self.state.current_conversation_id

        try:
            history = self.client.get_history(conversation_id)
        except ApiError as exc:
            self._show_error(str(exc))
            return

        if previous_conversation_id and previous_conversation_id != conversation_id:
            self.state.attached_paths.clear()
            self._update_attachment_label()

        self.state.current_conversation_id = conversation_id
        messages = [
            ChatMessage(
                role=msg.role,
                text=self._extract_text(msg),
                created_at=msg.created_at,
            )
            for msg in history
        ]
        self.state.set_messages(messages)
        self._render_messages()

    def _send_message(self) -> None:
        if self.stream_worker and self.stream_worker.isRunning():
            return

        prompt = self.input_box.toPlainText().strip()
        if not prompt:
            return

        conversation_id = self.state.current_conversation_id or str(uuid.uuid4())
        self.state.current_conversation_id = conversation_id

        request = ChatRequest(
            conversation_id=conversation_id,
            instructions=self.instructions_input.text().strip() or None,
            input=prompt,
            model=self.model_input.currentText().strip() or "gemini-2.0-flash-exp",
            file_paths=list(self.state.attached_paths),
        )

        self.state.add_message(role="user", text=prompt)
        self.state.add_message(role="assistant", text="")
        self._render_messages()

        self.input_box.clear()
        self.send_button.setEnabled(False)
        self.statusBar().showMessage("Đang tạo phản hồi...")

        self.stream_worker = ChatStreamWorker(self.client, request)
        self.stream_worker.chunk_received.connect(self._on_chunk_received)
        self.stream_worker.success.connect(self._on_stream_success)
        self.stream_worker.failed.connect(self._on_stream_failed)
        self.stream_worker.finished.connect(self._on_stream_finished)
        self.stream_worker.start()

    def _on_chunk_received(self, chunk: str) -> None:
        self.state.append_or_create_assistant_chunk(chunk)
        self._render_messages()

    def _on_stream_success(self, result: StreamResult) -> None:
        if self.state.messages and self.state.messages[-1].role == "assistant":
            self.state.messages[-1].text = result.text
        else:
            self.state.add_message(role="assistant", text=result.text)

        self.state.current_conversation_id = result.conversation_id
        self._render_messages()

        self._load_conversations()
        if self.state.attached_paths:
            self.statusBar().showMessage(
                f"Hoàn tất ({result.status}) • giữ lại {len(self.state.attached_paths)} tệp đính kèm",
                4000,
            )
        else:
            self.statusBar().showMessage(f"Hoàn tất ({result.status})", 4000)

    def _on_stream_failed(self, error_message: str) -> None:
        if self.state.messages and self.state.messages[-1].role == "assistant" and not self.state.messages[-1].text:
            self.state.messages.pop()
        self._render_messages()
        self._show_error(error_message)

    def _on_stream_finished(self) -> None:
        self.send_button.setEnabled(True)

    def _attach_files(self) -> None:
        file_paths, _ = QFileDialog.getOpenFileNames(self, "Chọn tệp")
        if not file_paths:
            return
        known = set(self.state.attached_paths)
        for path in file_paths:
            if path not in known:
                self.state.attached_paths.append(path)
        self._update_attachment_label()

    def _clear_attachments(self) -> None:
        self.state.attached_paths.clear()
        self._update_attachment_label()

    def _update_attachment_label(self) -> None:
        if not self.state.attached_paths:
            self.attachment_label.setText("Chưa có tệp đính kèm")
            self.attachment_label.setToolTip("")
            return
        count = len(self.state.attached_paths)
        short_names = [path.split("/")[-1].split("\\")[-1] for path in self.state.attached_paths]
        display = ", ".join(short_names[:2])
        if count > 2:
            display += f" +{count - 2} tệp nữa"
        self.attachment_label.setText(display)
        self.attachment_label.setToolTip("\n".join(self.state.attached_paths))

    def _render_messages(self) -> None:
        blocks: list[str] = [
            (
                "<html><body style='margin:0; font-family:Segoe UI, Arial, sans-serif; "
                "font-size:13px; color:#111827;'>"
            )
        ]

        for message_index, message in enumerate(self.state.messages):
            role = message.role.lower()
            title = "Bạn" if role == "user" else "Trợ lý"
            text = self._render_markdown_html(message.text)

            timestamp = ""
            if isinstance(message.created_at, datetime):
                timestamp = message.created_at.strftime("%H:%M")

            if role == "user":
                align = "right"
                bubble_background = "#e7f0ff"
                bubble_border = "#bfd4ff"
                title_color = "#1e3a8a"
            else:
                align = "left"
                bubble_background = "#ffffff"
                bubble_border = "#dfe3ea"
                title_color = "#374151"

            actions_html = ""
            if role == "assistant" and message.text.strip():
                actions_html = (
                    "<table cellspacing='0' cellpadding='0' style='margin-top:8px;'>"
                    "<tr>"
                    "<td style='background:#eef2ff; border:1px solid #c7d2fe; border-radius:6px; padding:4px 8px;'>"
                    f"<a href='action://export-word/{message_index}' style='text-decoration:none; color:#1e3a8a; font-weight:600;'>"
                    "📄 Xuất file Word</a>"
                    "</td>"
                    "<td style='width:20px; min-width:20px;'>&nbsp;</td>"
                    "<td style='background:#ecfeff; border:1px solid #a5f3fc; border-radius:6px; padding:4px 8px;'>"
                    f"<a href='action://export-pdf/{message_index}' style='text-decoration:none; color:#155e75; font-weight:600;'>"
                    "📕 Xuất file PDF</a>"
                    "</td>"
                    "</tr>"
                    "</table>"
                )

            bubble = (
                "<table width='100%' cellspacing='0' cellpadding='0' style='margin:0 0 10px 0;'>"
                f"<tr><td align='{align}'>"
                f"<table cellspacing='0' cellpadding='0' width='78%' style='background:{bubble_background}; "
                f"border:1px solid {bubble_border}; border-radius:10px;'>"
                "<tr><td style='padding:8px 10px 6px 10px;'>"
                f"<div style='font-weight:700; color:{title_color}; margin-bottom:4px;'>{title}</div>"
                f"<div style='line-height:1.42; color:#111827;'>{text}</div>"
                f"{actions_html}"
                f"<div style='font-size:11px; color:#6b7280; margin-top:6px;'>{timestamp}</div>"
                "</td></tr></table>"
                "</td></tr></table>"
            )
            blocks.append(bubble)

        blocks.append("</body></html>")
        self.chat_view.setHtml("".join(blocks))
        self.chat_view.verticalScrollBar().setValue(self.chat_view.verticalScrollBar().maximum())

    def _extract_text(self, message: BaseMessage) -> str:
        content_text = message.content.get("text", "")
        if isinstance(content_text, str):
            return content_text
        return str(content_text)

    def _on_chat_link_clicked(self, url: QUrl) -> None:
        if url.scheme() != "action":
            return

        action = url.host().strip().lower()
        raw_index = url.path().lstrip("/")
        try:
            message_index = int(raw_index)
        except ValueError:
            self._show_error("Liên kết thao tác không hợp lệ.")
            return

        if message_index < 0 or message_index >= len(self.state.messages):
            self._show_error("Không tìm thấy phản hồi cần xuất.")
            return

        message = self.state.messages[message_index]
        if message.role.lower() != "assistant" or not message.text.strip():
            self._show_error("Chỉ có thể xuất phản hồi của Trợ lý.")
            return

        if action == "export-word":
            self._export_assistant_message_to_word(message)
            return

        if action == "export-pdf":
            self._export_assistant_message_to_pdf(message)
            return

        self._show_error("Thao tác chưa được hỗ trợ.")

    def _render_markdown_html(self, text: str) -> str:
        normalized = text.strip()
        if not normalized:
            return ""

        document = QTextDocument()
        document.setMarkdown(normalized)
        rendered = document.toHtml()

        body_start = rendered.find("<body")
        if body_start < 0:
            return html.escape(text).replace("\n", "<br/>")

        body_open_end = rendered.find(">", body_start)
        body_end = rendered.rfind("</body>")
        if body_open_end < 0 or body_end <= body_open_end:
            return html.escape(text).replace("\n", "<br/>")

        body_html = rendered[body_open_end + 1:body_end].strip()
        if not body_html:
            return html.escape(text).replace("\n", "<br/>")

        return body_html

    def _export_conversation_to_word(self) -> None:
        assistant_messages = [
            message
            for message in self.state.messages
            if message.role.lower() == "assistant" and message.text.strip()
        ]
        if not assistant_messages:
            QMessageBox.information(
                self,
                "Xuất Word",
                "Chưa có phản hồi Trợ lý để xuất.",
            )
            return

        latest_assistant_message = assistant_messages[-1]

        self._export_assistant_message_to_word(latest_assistant_message)

    def _export_assistant_message_to_word(self, assistant_message: ChatMessage) -> None:
        if assistant_message.role.lower() != "assistant" or not assistant_message.text.strip():
            QMessageBox.information(
                self,
                "Xuất Word",
                "Phản hồi Trợ lý không có nội dung để xuất.",
            )
            return

        try:
            from docx import Document
            from htmldocx import HtmlToDocx
            import markdown
        except Exception:
            self._show_error(
                "Thiếu thư viện xuất Word. Hãy cài `python-docx`, `markdown`, `htmldocx` rồi thử lại."
            )
            return

        title = self._current_conversation_title() or "Cuộc trò chuyện"
        default_stem = self._safe_filename(f"{title}_tro_ly_{datetime.now().strftime('%Y%m%d_%H%M')}")
        default_dir = Path.home() / "Documents"
        if not default_dir.exists():
            default_dir = Path.home()
        default_path = default_dir / f"{default_stem}.docx"

        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "Lưu file Word",
            str(default_path),
            "Word Document (*.docx)",
        )
        if not file_path:
            return

        output_path = Path(file_path)
        if output_path.suffix.lower() != ".docx":
            output_path = output_path.with_suffix(".docx")

        markdown_text = assistant_message.text.strip()
        if not markdown_text:
            QMessageBox.information(
                self,
                "Xuất Word",
                "Phản hồi Trợ lý không có nội dung để xuất.",
            )
            return

        html_content = markdown.markdown(
            markdown_text,
            extensions=["fenced_code", "tables", "sane_lists", "nl2br"],
        )

        document = Document()
        html_parser = HtmlToDocx()
        html_parser.add_html_to_document(html_content, document)

        if not document.paragraphs and not document.tables:
            document.add_paragraph(markdown_text)

        try:
            document.save(str(output_path))
        except Exception as exc:
            self._show_error(f"Không thể lưu file Word: {exc}")
            return

        self.statusBar().showMessage(f"Đã xuất 1 phản hồi Trợ lý: {output_path.name}", 4000)

    def _export_assistant_message_to_pdf(self, assistant_message: ChatMessage) -> None:
        if assistant_message.role.lower() != "assistant" or not assistant_message.text.strip():
            QMessageBox.information(
                self,
                "Xuất PDF",
                "Phản hồi Trợ lý không có nội dung để xuất.",
            )
            return

        title = self._current_conversation_title() or "Cuộc trò chuyện"
        default_stem = self._safe_filename(f"{title}_tro_ly_{datetime.now().strftime('%Y%m%d_%H%M')}")
        default_dir = Path.home() / "Documents"
        if not default_dir.exists():
            default_dir = Path.home()
        default_path = default_dir / f"{default_stem}.pdf"

        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "Lưu file PDF",
            str(default_path),
            "PDF Document (*.pdf)",
        )
        if not file_path:
            return

        output_path = Path(file_path)
        if output_path.suffix.lower() != ".pdf":
            output_path = output_path.with_suffix(".pdf")

        markdown_text = assistant_message.text.strip()
        document = QTextDocument()
        document.setMarkdown(markdown_text)

        printer = QPrinter(QPrinter.PrinterMode.HighResolution)
        printer.setOutputFormat(QPrinter.OutputFormat.PdfFormat)
        printer.setOutputFileName(str(output_path))

        try:
            document.print(printer)
        except Exception as exc:
            self._show_error(f"Không thể lưu file PDF: {exc}")
            return

        self.statusBar().showMessage(f"Đã xuất 1 phản hồi Trợ lý: {output_path.name}", 4000)

    def _current_conversation_title(self) -> str | None:
        current_item = self.conversation_list.currentItem()
        if current_item is not None:
            maybe_title = current_item.data(Qt.ItemDataRole.UserRole + 1)
            if isinstance(maybe_title, str) and maybe_title.strip():
                return maybe_title.strip()

        if self.state.current_conversation_id:
            return f"Cuộc trò chuyện {self.state.current_conversation_id[:8]}"
        return None

    def _safe_filename(self, value: str) -> str:
        cleaned = re.sub(r"[\\/:*?\"<>|]+", "_", value).strip().strip(".")
        return cleaned or "cuoc_tro_chuyen"

    def _show_error(self, message: str) -> None:
        self.statusBar().showMessage("Lỗi", 4000)
        QMessageBox.critical(self, "Lỗi", message)

    def _conversation_label(self, conversation: Conversation) -> str:
        if conversation.title:
            return conversation.title

        created = conversation.created_at.strftime("%Y-%m-%d %H:%M")
        return f"{created}  •  {conversation.id[:8]}..."
