namespace ChatbotChaytauDesktop
{
    partial class ChatbotChaytauForm
    {
        private System.ComponentModel.IContainer components = null;
        private Panel sidebarPanel;
        private Label apiKeyLabel;
        private TextBox apiKeyTextBox;
        private Label roleLabel;
        private ComboBox roleComboBox;
        private Label branchLabel;
        private ComboBox branchComboBox;
        private Label taskLabel;
        private ComboBox taskComboBox;
        private Label conversationsLabel;
        private ListBox conversationsListBox;
        private Panel chatPanel;
        private FlowLayoutPanel chatFlowPanel;
        private Panel chatInputPanel;
        private TextBox chatInputTextBox;
        private Button uploadButton;
        private Button sendButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            sidebarPanel = new Panel();
            apiKeyLabel = new Label();
            apiKeyTextBox = new TextBox();
            roleLabel = new Label();
            roleComboBox = new ComboBox();
            branchLabel = new Label();
            branchComboBox = new ComboBox();
            taskLabel = new Label();
            taskComboBox = new ComboBox();
            conversationsLabel = new Label();
            conversationsListBox = new ListBox();
            chatPanel = new Panel();
            chatFlowPanel = new FlowLayoutPanel();
            chatInputPanel = new Panel();
            chatInputTextBox = new TextBox();
            uploadButton = new Button();
            sendButton = new Button();
            sidebarPanel.SuspendLayout();
            chatPanel.SuspendLayout();
            chatInputPanel.SuspendLayout();
            SuspendLayout();
            // 
            // sidebarPanel
            // 
            sidebarPanel.Controls.Add(conversationsListBox);
            sidebarPanel.Controls.Add(conversationsLabel);
            sidebarPanel.Controls.Add(taskComboBox);
            sidebarPanel.Controls.Add(taskLabel);
            sidebarPanel.Controls.Add(branchComboBox);
            sidebarPanel.Controls.Add(branchLabel);
            sidebarPanel.Controls.Add(roleComboBox);
            sidebarPanel.Controls.Add(roleLabel);
            sidebarPanel.Controls.Add(apiKeyTextBox);
            sidebarPanel.Controls.Add(apiKeyLabel);
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Padding = new Padding(12);
            sidebarPanel.Width = 280;
            sidebarPanel.BackColor = SystemColors.ControlLight;
            // 
            // apiKeyLabel
            // 
            apiKeyLabel.AutoSize = true;
            apiKeyLabel.Location = new Point(12, 12);
            apiKeyLabel.Text = "Nhập API Key";
            // 
            // apiKeyTextBox
            // 
            apiKeyTextBox.Location = new Point(12, 34);
            apiKeyTextBox.Size = new Size(252, 23);
            apiKeyTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // roleLabel
            // 
            roleLabel.AutoSize = true;
            roleLabel.Location = new Point(12, 68);
            roleLabel.Text = "Nhập vai trò";
            // 
            // roleComboBox
            // 
            roleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            roleComboBox.Location = new Point(12, 90);
            roleComboBox.Size = new Size(252, 23);
            roleComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // branchLabel
            // 
            branchLabel.AutoSize = true;
            branchLabel.Location = new Point(12, 124);
            branchLabel.Text = "Nhập chi nhánh";
            // 
            // branchComboBox
            // 
            branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            branchComboBox.Location = new Point(12, 146);
            branchComboBox.Size = new Size(252, 23);
            branchComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // taskLabel
            // 
            taskLabel.AutoSize = true;
            taskLabel.Location = new Point(12, 180);
            taskLabel.Text = "Nhập tác vụ";
            // 
            // taskComboBox
            // 
            taskComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            taskComboBox.Location = new Point(12, 202);
            taskComboBox.Size = new Size(252, 23);
            taskComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // conversationsLabel
            // 
            conversationsLabel.AutoSize = true;
            conversationsLabel.Location = new Point(12, 236);
            conversationsLabel.Text = "Danh sách hội thoại";
            // 
            // conversationsListBox
            // 
            conversationsListBox.Location = new Point(12, 258);
            conversationsListBox.Size = new Size(252, 390);
            conversationsListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // chatPanel
            // 
            chatPanel.Controls.Add(chatFlowPanel);
            chatPanel.Controls.Add(chatInputPanel);
            chatPanel.Dock = DockStyle.Fill;
            // 
            // chatFlowPanel
            // 
            chatFlowPanel.Dock = DockStyle.Fill;
            chatFlowPanel.FlowDirection = FlowDirection.TopDown;
            chatFlowPanel.WrapContents = false;
            chatFlowPanel.AutoScroll = true;
            chatFlowPanel.Padding = new Padding(16, 12, 16, 12);
            chatFlowPanel.BackColor = SystemColors.Window;
            // 
            // chatInputPanel
            // 
            chatInputPanel.Controls.Add(chatInputTextBox);
            chatInputPanel.Controls.Add(uploadButton);
            chatInputPanel.Controls.Add(sendButton);
            chatInputPanel.Dock = DockStyle.Bottom;
            chatInputPanel.Padding = new Padding(8);
            chatInputPanel.Height = 70;
            // 
            // chatInputTextBox
            // 
            chatInputTextBox.Dock = DockStyle.Fill;
            chatInputTextBox.Multiline = true;
            // 
            // uploadButton
            // 
            uploadButton.Dock = DockStyle.Left;
            uploadButton.Text = "Upload";
            uploadButton.Width = 90;
            // 
            // sendButton
            // 
            sendButton.Dock = DockStyle.Right;
            sendButton.Text = "Send";
            sendButton.Width = 90;
            sendButton.Click += sendButton_Click;
            // 
            // ChatbotChaytauForm
            // 
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 800);
            Controls.Add(chatPanel);
            Controls.Add(sidebarPanel);
            Text = "Chatbot Chaytau";
            sidebarPanel.ResumeLayout(false);
            sidebarPanel.PerformLayout();
            chatPanel.ResumeLayout(false);
            chatInputPanel.ResumeLayout(false);
            chatInputPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
