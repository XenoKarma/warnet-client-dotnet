using System.Runtime.InteropServices;

namespace WarnetClient;

public partial class LockScreenForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private readonly string _pcNumber;
    private Label _statusLabel = null!;
    private Label _pcLabel = null!;
    private Label _timerLabel = null!;
    private Label _subLabel = null!;
    private Label _notificationLabel = null!;
    private System.Windows.Forms.Timer _blinkTimer = null!;
    private System.Windows.Forms.Timer _notifTimer = null!;

    private bool _locked = true;
    private bool _blinkState = true;
    private readonly bool _debug;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;
            return cp;
        }
    }

    public LockScreenForm(string pcNumber, bool debug = false)
    {
        _pcNumber = pcNumber;
        _debug = debug;
        BuildUI();
    }

    private void BuildUI()
    {
        Text = $"Warnet Client - {_pcNumber}";
        BackColor = Color.FromArgb(5, 5, 8);
        DoubleBuffered = true;
        Cursor = Cursors.Default;
        KeyPreview = true;

        if (_debug)
        {
            // DEBUG: window biasa, bisa di-resize, Alt+F4 works
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            TopMost = false;
            ShowInTaskbar = true;
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Text += " [DEBUG]";
        }
        else
        {
            // PRODUCTION: fullscreen tanpa border, tidak bisa ditutup
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
        }

        // Escape: Ctrl+Shift+X untuk tutup (semua mode)
        KeyDown += (_, e) =>
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.X)
            {
                Application.Exit();
                return;
            }
            if (!_debug && (e.Alt || e.Control || e.KeyCode == Keys.F4))
                e.SuppressKeyPress = true;
        };

        // ===== PC NUMBER (besar di tengah) =====
        _pcLabel = new Label
        {
            Text = _pcNumber.ToUpper(),
            ForeColor = Color.FromArgb(0, 229, 255),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 64, FontStyle.Bold),
            Dock = DockStyle.None,
            Size = new Size(600, 100),
            Location = new Point((Screen.PrimaryScreen!.Bounds.Width - 600) / 2, 180),
        };
        Controls.Add(_pcLabel);

        // ===== STATUS =====
        _statusLabel = new Label
        {
            Text = "● TERKUNCI",
            ForeColor = Color.FromArgb(255, 23, 68),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            Dock = DockStyle.None,
            Size = new Size(400, 50),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 400) / 2, 290),
        };
        Controls.Add(_statusLabel);

        // ===== TIMER =====
        _timerLabel = new Label
        {
            Text = "--:--:--",
            ForeColor = Color.FromArgb(0, 229, 255),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 72, FontStyle.Bold),
            Dock = DockStyle.None,
            Size = new Size(500, 120),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 500) / 2, 360),
            Visible = false,
        };
        Controls.Add(_timerLabel);

        // ===== SUBTEXT =====
        _subLabel = new Label
        {
            Text = "Menunggu operator untuk memulai sesi billing...",
            ForeColor = Color.FromArgb(80, 80, 90),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14, FontStyle.Regular),
            Dock = DockStyle.None,
            Size = new Size(600, 30),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 600) / 2, 510),
        };
        Controls.Add(_subLabel);

        // ===== FOOTER =====
        var footer = new Label
        {
            Text = "WARNET BILLING SYSTEM v1.0",
            ForeColor = Color.FromArgb(40, 40, 50),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Dock = DockStyle.None,
            Size = new Size(400, 30),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 400) / 2, Screen.PrimaryScreen.Bounds.Height - 80),
        };
        Controls.Add(footer);

        // ===== NOTIFICATION (floating, muncul sebentar) =====
        _notificationLabel = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(0, 230, 118),
            BackColor = Color.FromArgb(20, 20, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Dock = DockStyle.None,
            Size = new Size(400, 50),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 400) / 2, 600),
            Visible = false,
        };
        Controls.Add(_notificationLabel);

        // ===== BLINK TIMER (animasi status blink) =====
        _blinkTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkState = !_blinkState;
            if (_locked)
                _statusLabel.ForeColor = _blinkState ? Color.FromArgb(255, 23, 68) : Color.FromArgb(100, 10, 20);
            Invalidate();
        };
        _blinkTimer.Start();

        // ===== NOTIF TIMER =====
        _notifTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _notifTimer.Tick += (_, _) =>
        {
            _notificationLabel.Visible = false;
            _notifTimer.Stop();
        };
    }

    // Gambar background gradient + grid
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var rect = ClientRectangle;

        // Gradient background
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            rect,
            Color.FromArgb(5, 5, 15),
            Color.FromArgb(10, 10, 25),
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        g.FillRectangle(brush, rect);

        // Grid effect
        using var gridPen = new Pen(Color.FromArgb(15, 15, 30));
        for (int x = 0; x < rect.Width; x += 60)
            g.DrawLine(gridPen, x, 0, x, rect.Height);
        for (int y = 0; y < rect.Height; y += 60)
            g.DrawLine(gridPen, 0, y, rect.Width, y);

        // Accent line bawah PC label
        int centerX = rect.Width / 2;
        using var accentPen = new Pen(Color.FromArgb(0, 229, 255, 60), 2);
        g.DrawLine(accentPen, centerX - 80, 285, centerX + 80, 285);

        // Glow orbs
        using var glowBrush = new SolidBrush(Color.FromArgb(8, 0, 229, 255));
        g.FillEllipse(glowBrush, rect.Width / 2 - 150, 100, 300, 300);
    }

    // ===== PUBLIC METHODS =====

    public void Lock(string reason = "")
    {
        _locked = true;
        _blinkTimer.Start();

        var reasons = new Dictionary<string, string>
        {
            { "Sesi dihentikan operator", "Sesi dihentikan oleh operator" },
            { "Waktu billing habis", "Waktu billing telah habis" },
            { "Koneksi server putus", "Koneksi ke server terputus" },
        };

        var subText = reasons.TryGetValue(reason, out var msg)
            ? msg
            : "Menunggu operator untuk memulai sesi billing...";

        BeginInvoke(() =>
        {
            _timerLabel.Visible = false;
            _statusLabel.Text = "● TERKUNCI";
            _statusLabel.ForeColor = Color.FromArgb(255, 23, 68);
            _subLabel.Text = subText;

            if (!string.IsNullOrEmpty(reason))
                _subLabel.ForeColor = Color.FromArgb(255, 180, 50);
            else
                _subLabel.ForeColor = Color.FromArgb(80, 80, 90);

            if (!_debug)
            {
                WindowState = FormWindowState.Maximized;
                TopMost = true;
            }

            Show();
            Activate();
            BringToFront();

            // Force window to foreground via Win32
            if (IsHandleCreated)
            {
                ShowWindowAsync(Handle, SW_RESTORE);
                SetForegroundWindow(Handle);
            }

            // Flash trick: temporal TopMost biar muncul di atas browser
            if (_debug)
            {
                TopMost = true;
                TopMost = false;
            }

            Cursor.Show();
        });
    }

    public void Unlock(int totalSeconds)
    {
        _locked = false;
        _blinkTimer.Stop();

        BeginInvoke(() =>
        {
            _statusLabel.Text = "● AKTIF";
            _statusLabel.ForeColor = Color.FromArgb(0, 230, 118);
            _timerLabel.Text = FormatTime(totalSeconds);
            _timerLabel.Visible = true;
            _subLabel.Text = "Sesi billing sedang berlangsung";
            _subLabel.ForeColor = Color.FromArgb(0, 229, 255, 120);

            Hide();
            if (!_debug) Cursor.Hide();
        });
    }

    public void UpdateTimer(int timeLeftSeconds, int durationSeconds)
    {
        if (!_locked)
        {
            BeginInvoke(() =>
            {
                _timerLabel.Text = FormatTime(Math.Max(0, timeLeftSeconds));

                // Warna merah jika < 5 menit
                if (timeLeftSeconds <= 300 && timeLeftSeconds > 0)
                    _timerLabel.ForeColor = Color.FromArgb(255, 23, 68);
                else
                    _timerLabel.ForeColor = Color.FromArgb(0, 229, 255);
            });
        }
    }

    public void ShowNotification(string message)
    {
        BeginInvoke(() =>
        {
            _notificationLabel.Text = message;
            _notificationLabel.Visible = true;
            _notificationLabel.BringToFront();
            _notifTimer.Start();
        });
    }

    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds <= 0) return "00:00:00";
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }
}
