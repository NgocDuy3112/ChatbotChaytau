using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ChatbotChaytauDesktop
{
    public partial class ChatbotChaytauForm : Form
    {
        private static readonly HttpClient HttpClient = new();
        private const string ChatEndpoint = "http://127.0.0.1:8000/chat/generate";

        public ChatbotChaytauForm()
        {
            InitializeComponent();
            chatFlowPanel.SizeChanged += chatFlowPanel_SizeChanged;
            StartBackend();
        }

        private static void StartBackend()
        {
            var backendDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "backend"));
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = """
                    -NoProfile -ExecutionPolicy Bypass -Command ". .\.venv\Scripts\Activate.ps1
                    cd app
                    fastapi run main.py --reload"
                """,
                WorkingDirectory = backendDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
        }

        private void chatFlowPanel_SizeChanged(object sender, EventArgs e)
        {
            foreach (Control control in chatFlowPanel.Controls)
            {
                if (control is Panel rowPanel)
                {
                    UpdateMessageRowLayout(rowPanel);
                }
            }
        }

        private async void sendButton_Click(object sender, EventArgs e)
        {
            var message = chatInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AddMessageBubble(message, isUser: true);
            chatInputTextBox.Clear();
            sendButton.Enabled = false;

            try
            {
                var payload = new ChatRequest { Message = message };
                var response = await HttpClient.PostAsJsonAsync(ChatEndpoint, payload);
                response.EnsureSuccessStatusCode();
                var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();

                AddMessageBubble(chatResponse?.Response ?? "(Không nhận được phản hồi)", isUser: false);
            }
            catch (Exception ex)
            {
                AddMessageBubble($"Lỗi gọi API: {ex.Message}", isUser: false);
            }
            finally
            {
                sendButton.Enabled = true;
            }
        }

        private void AddMessageBubble(string message, bool isUser)
        {
            var bubblePanel = new Panel
            {
                AutoSize = false,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0),
                BackColor = isUser ? Color.FromArgb(0, 120, 212) : Color.FromArgb(230, 230, 230),
                MaximumSize = new Size(chatFlowPanel.ClientSize.Width - 60, 0)
            };

            var contentPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = isUser ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            var iconLabel = new Label
            {
                AutoSize = true,
                Text = isUser ? "User" : "AI",
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                ForeColor = isUser ? Color.White : Color.DimGray,
                Margin = new Padding(0, 2, 8, 0)
            };

            var messageLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(bubblePanel.MaximumSize.Width - 52, 0),
                ForeColor = isUser ? Color.White : Color.Black,
                Text = message
            };

            contentPanel.Controls.Add(iconLabel);
            contentPanel.Controls.Add(messageLabel);
            bubblePanel.Controls.Add(contentPanel);
            bubblePanel.Tag = isUser;

            var rowPanel = new Panel
            {
                AutoSize = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            rowPanel.Controls.Add(bubblePanel);
            UpdateMessageRowLayout(rowPanel);
            chatFlowPanel.Controls.Add(rowPanel);
            chatFlowPanel.ScrollControlIntoView(rowPanel);
        }

        private void UpdateMessageRowLayout(Panel rowPanel)
        {
            var availableWidth = Math.Max(0, chatFlowPanel.ClientSize.Width - chatFlowPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            rowPanel.Width = availableWidth;

            if (rowPanel.Controls.Count == 0)
            {
                return;
            }

            var bubblePanel = rowPanel.Controls[0] as Panel;
            if (bubblePanel == null)
            {
                return;
            }

            var maxBubbleWidth = Math.Max(0, availableWidth - 40);
            bubblePanel.MaximumSize = new Size(maxBubbleWidth, 0);
            var preferredSize = bubblePanel.PreferredSize;
            bubblePanel.Size = new Size(Math.Min(preferredSize.Width, maxBubbleWidth), preferredSize.Height);

            foreach (Control content in bubblePanel.Controls)
            {
                if (content is FlowLayoutPanel contentPanel)
                {
                    foreach (Control item in contentPanel.Controls)
                    {
                        if (item is Label label)
                        {
                            label.MaximumSize = new Size(Math.Max(0, bubblePanel.MaximumSize.Width - 52), 0);
                        }
                    }
                }
            }

            var isUser = bubblePanel.Tag is bool tag && tag;
            bubblePanel.Location = new Point(isUser ? Math.Max(0, availableWidth - bubblePanel.Width) : 0, 0);
            rowPanel.Height = bubblePanel.Height;
        }

        private sealed class ChatRequest
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;
        }

        private sealed class ChatResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
        }
    }
}
