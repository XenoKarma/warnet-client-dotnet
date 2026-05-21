using System.Text.Json;
using SocketIOClient;

namespace WarnetClient;

public class MainForm : Form
{
    private SocketIOClient.SocketIO? _socket;
    private LockScreenForm _lockScreen = null!;
    private TimerOverlayForm _timerOverlay = null!;
    private NotifyIcon _trayIcon;
    private ContextMenuStrip _trayMenu;
    private bool _sessionActive = false;
    private static readonly string _logPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "warnet-client.log");

    public MainForm()
    {
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Load += OnLoad;
        FormClosing += OnFormClosing;

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Tampilkan", null, (_, _) => ShowLockScreen());
        _trayMenu.Items.Add("Keluar", null, (_, _) => Application.Exit());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"Warnet Client - {Program.AppConfig.pcNumber}",
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowLockScreen();
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        var debug = Program.AppConfig.debug;

        // Lock screen langsung muncul begitu app jalan (PC terkunci sampai login)
        _lockScreen = new LockScreenForm(Program.AppConfig.pcNumber, debug);
        _lockScreen.Show();

        _timerOverlay = new TimerOverlayForm(Program.AppConfig.pcNumber, debug);

        await ConnectToServer();
    }

    private async Task ConnectToServer()
    {
        _socket = new SocketIOClient.SocketIO(Program.AppConfig.serverUrl, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
        });

        _socket.OnConnected += async (_, _) =>
        {
            Log("Terhubung ke server");
            try
            {
                await _socket.EmitAsync("register-pc", new { pc_number = Program.AppConfig.pcNumber });
                Log($"Registrasi {Program.AppConfig.pcNumber} dikirim");
                _lockScreen.ShowNotification("Server terhubung");
            }
            catch (Exception ex) { Log($"ERROR register-pc: {ex.Message}"); }
        };

        // BEST PRACTICE: Koneksi putus → kunci PC (biar gak gratis)
        _socket.OnDisconnected += (_, _) =>
        {
            Log("Terputus dari server");
            if (_sessionActive)
            {
                BeginInvoke(() =>
                {
                    LockPC("Koneksi server putus");
                });
            }
        };

        // BEST PRACTICE: timer mencapai 0 → auto-lock (fallback kalau stop-session gak sampe)
        _socket.On("timer-update", response =>
        {
            try
            {
                var data = response.GetValue<TimerData>();
                if (data == null) return;

                BeginInvoke(() =>
                {
                    try
                    {
                        if (data.timeLeftSeconds <= 0 && _sessionActive)
                        {
                            Log("Waktu habis — auto lock");
                            LockPC("Waktu billing habis");
                            return;
                        }

                        _lockScreen.UpdateTimer(data.timeLeftSeconds, data.durationSeconds);
                        _timerOverlay.UpdateTimer(data.timeLeftSeconds, data.durationSeconds);
                    }
                    catch (Exception ex) { Log($"ERROR timer-update UI: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Log($"ERROR timer-update: {ex.Message}"); }
        });

        _socket.On("start-session", response =>
        {
            try
            {
                var data = response.GetValue<SessionData>();
                var minutes = data?.duration_minutes ?? 0;
                Log($"Sesi DIMULAI - Durasi: {minutes} menit");

                BeginInvoke(() =>
                {
                    try
                    {
                        _sessionActive = true;
                        _lockScreen.Unlock(minutes * 60);
                        _timerOverlay.ShowTimer();
                        _timerOverlay.UpdateTimer(minutes * 60, minutes * 60);
                        _trayIcon.Text = $"Warnet Client - {Program.AppConfig.pcNumber} [AKTIF]";
                    }
                    catch (Exception ex) { Log($"ERROR start-session UI: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Log($"ERROR start-session: {ex.Message}"); }
        });

        _socket.On("stop-session", _ =>
        {
            try
            {
                Log("Sesi DIHENTIKAN");
                BeginInvoke(() =>
                {
                    try
                    {
                        LockPC("Sesi dihentikan operator");
                    }
                    catch (Exception ex) { Log($"ERROR stop-session UI: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Log($"ERROR stop-session: {ex.Message}"); }
        });

        _socket.On("add-time", response =>
        {
            try
            {
                var data = response.GetValue<AddTimeData>();
                Log($"Waktu ditambah: {data?.added_minutes} menit");

                BeginInvoke(() =>
                {
                    try
                    {
                        _lockScreen.ShowNotification($"+{data?.added_minutes} menit");
                        _timerOverlay.ShowTimer();
                    }
                    catch (Exception ex) { Log($"ERROR add-time UI: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Log($"ERROR add-time: {ex.Message}"); }
        });

        try
        {
            await _socket.ConnectAsync();
        }
        catch (Exception ex)
        {
            Log($"Gagal konek: {ex.Message}");
            _lockScreen.ShowNotification("Server tidak terjangkau");
        }
    }

    // BEST PRACTICE: 1 fungsi buat semua skenario lock
    private void LockPC(string reason = "")
    {
        if (!_sessionActive)
        {
            Log($"LockPC SKIP: _sessionActive = false (alasan: {reason})");
            return;
        }
        _sessionActive = false;

        Log($"LockPC EKSEKUSI: {reason}");
        _timerOverlay.HideTimer();
        _lockScreen.Lock(reason);
        _trayIcon.Text = $"Warnet Client - {Program.AppConfig.pcNumber}";
        Log($"PC terkunci: {reason}");
    }

    private void ShowLockScreen()
    {
        if (_lockScreen == null || _lockScreen.IsDisposed) return;
        _lockScreen.Show();
        _lockScreen.WindowState = FormWindowState.Normal;
        _lockScreen.BringToFront();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _timerOverlay?.Close();
        _socket?.DisconnectAsync();
    }

    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Console.WriteLine(line);
        try { File.AppendAllText(_logPath, line + Environment.NewLine); }
        catch { }
    }
}

public class SessionData
{
    public int duration_minutes { get; set; }
}

public class TimerData
{
    public int timeLeftSeconds { get; set; }
    public int durationSeconds { get; set; }
}

public class AddTimeData
{
    public int added_minutes { get; set; }
}
