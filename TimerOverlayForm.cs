namespace WarnetClient;

public class TimerOverlayForm : Form
{
    private Label _pcLabel = null!;
    private Label _timerLabel = null!;
    private Label _statusLabel = null!;
    private bool _debug;

    public TimerOverlayForm(string pcNumber, bool debug = false)
    {
        _debug = debug;
        Text = $"Timer - {pcNumber}";

        if (!debug)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = true;
            TopMost = false;
            Text += " [DEBUG]";
        }

        BackColor = Color.FromArgb(10, 10, 20);
        StartPosition = FormStartPosition.Manual;

        // Pojok kanan bawah
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - 300, screen.Bottom - 100);
        Size = new Size(290, 90);

        // ===== PC LABEL =====
        _pcLabel = new Label
        {
            Text = pcNumber.ToUpper(),
            ForeColor = Color.FromArgb(0, 229, 255),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(12, 8),
            Size = new Size(80, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(_pcLabel);

        // ===== STATUS DOT =====
        _statusLabel = new Label
        {
            Text = "● AKTIF",
            ForeColor = Color.FromArgb(0, 230, 118),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Location = new Point(12, 30),
            Size = new Size(60, 16),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(_statusLabel);

        // ===== TIMER =====
        _timerLabel = new Label
        {
            Text = "00:00:00",
            ForeColor = Color.FromArgb(0, 229, 255),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            Location = new Point(75, 10),
            Size = new Size(200, 50),
            TextAlign = ContentAlignment.MiddleRight,
        };
        Controls.Add(_timerLabel);

        // ===== HIDE ON CLOSE (instead of closing) =====
        if (!debug)
        {
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // Border tipis glow
        using var borderPen = new Pen(Color.FromArgb(0, 229, 255, 40), 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // Accent line atas
        using var accentPen = new Pen(Color.FromArgb(0, 229, 255, 80), 2);
        g.DrawLine(accentPen, 0, 0, Width, 0);
    }

    public void UpdateTimer(int timeLeftSeconds, int durationSeconds)
    {
        BeginInvoke(() =>
        {
            var total = Math.Max(0, timeLeftSeconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            _timerLabel.Text = $"{h:D2}:{m:D2}:{s:D2}";

            // Warna merah jika < 5 menit
            if (total <= 300 && total > 0)
                _timerLabel.ForeColor = Color.FromArgb(255, 23, 68);
            else
                _timerLabel.ForeColor = Color.FromArgb(0, 229, 255);

            // Status blink jika < 1 menit
            if (total <= 60 && total > 0)
                _statusLabel.Text = "⚠️ HABIS";
            else
                _statusLabel.Text = "● AKTIF";
        });
    }

    public void ShowTimer()
    {
        if (_debug)
        {
            Show();
            BringToFront();
            return;
        }

        // Posisi di pojok kanan bawah
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - 300, screen.Bottom - 100);

        if (!Visible)
        {
            Show();
        }
        BringToFront();
    }

    public void HideTimer()
    {
        Hide();
    }
}
