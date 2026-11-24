using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MercadoInteligente
{
    static class Launcher
    {
        private const string HealthUrl = "http://127.0.0.1:5000/api/v1/system/health";
        private const string AppUrl = "http://127.0.0.1:5000";

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string runtimeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MercadoInteligente", "runtime");
                string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MercadoInteligente", "logs");

                if (!Directory.Exists(runtimeDir)) Directory.CreateDirectory(runtimeDir);
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

                string pidFile = Path.Combine(runtimeDir, "app.pid");
                string logFile = Path.Combine(logsDir, "launcher.log");

                Action<string> log = (msg) =>
                {
                    try
                    {
                        string line = string.Format("[{0:o}] [NativeLauncher] {1}{2}", DateTime.UtcNow, msg, Environment.NewLine);
                        File.AppendAllText(logFile, line, Encoding.UTF8);
                    }
                    catch { }
                };

                log("Launcher iniciado.");

                bool alreadyRunning = IsServerHealthy();
                if (!alreadyRunning)
                {
                    string serverExe = FindServerExecutable(baseDir);
                    if (string.IsNullOrEmpty(serverExe) || !File.Exists(serverExe))
                    {
                        MessageBox.Show(
                            "No se pudo encontrar el servidor ejecutable de Mercado Inteligente en:\n" +
                            Path.Combine(baseDir, @"release\MercadoInteligente\MercadoInteligente.exe"),
                            "Mercado Inteligente - Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        return;
                    }

                    log("Iniciando servidor: " + serverExe);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = serverExe,
                        WorkingDirectory = Path.GetDirectoryName(serverExe),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = true
                    };

                    Process serverProc = Process.Start(psi);
                    if (serverProc != null)
                    {
                        try
                        {
                            File.WriteAllText(pidFile, serverProc.Id.ToString(), Encoding.ASCII);
                            log("Servidor iniciado con PID: " + serverProc.Id);
                        }
                        catch { }
                    }

                    // Esperar a que el servidor responda al health check (máximo 15 segundos)
                    bool healthy = false;
                    for (int i = 0; i < 30; i++)
                    {
                        Thread.Sleep(500);
                        if (IsServerHealthy())
                        {
                            healthy = true;
                            break;
                        }
                    }

                    if (!healthy)
                    {
                        log("Tiempo de espera agotado esperando que el servidor responda.");
                        MessageBox.Show(
                            "El servidor de Mercado Inteligente se inició pero tardó demasiado en responder el health check.\nRevisa los logs en %LOCALAPPDATA%\\MercadoInteligente\\logs",
                            "Mercado Inteligente - Advertencia",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                    }
                    else
                    {
                        log("Servidor verificado y saludable.");
                    }
                }
                else
                {
                    log("El servidor ya se encontraba activo.");
                }

                // Abrir en el navegador preferido / modo PWA app
                bool forceBrowserTab = false;
                foreach (string a in args)
                {
                    if (string.Equals(a, "--tab", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "--browser", StringComparison.OrdinalIgnoreCase))
                    {
                        forceBrowserTab = true;
                    }
                }

                OpenApp(forceBrowserTab, log);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al iniciar Mercado Inteligente:\n" + ex.Message,
                    "Mercado Inteligente - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private static bool IsServerHealthy()
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(HealthUrl);
                req.Method = "GET";
                req.Timeout = 1200;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                        {
                            string content = reader.ReadToEnd();
                            return content.Contains("\"database\":\"ok\"") || content.Contains("\"ok\"");
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static string FindServerExecutable(string baseDir)
        {
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, @"release\MercadoInteligente\MercadoInteligente.exe"),
                Path.Combine(baseDir, @"release\MercadoInteligente\MercadoInteligente.Api.exe"),
                Path.Combine(baseDir, @"src\backend\MercadoInteligente.Api\bin\Release\net10.0\win-x64\publish\MercadoInteligente.exe"),
                Path.Combine(baseDir, @"src\backend\MercadoInteligente.Api\bin\Debug\net10.0\MercadoInteligente.Api.exe"),
                Path.Combine(baseDir, @"MercadoInteligente.exe")
            };

            foreach (string c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }

        private static void OpenApp(bool forceBrowserTab, Action<string> log)
        {
            if (!forceBrowserTab)
            {
                // 1. Probar Google Chrome en modo aplicación (--app)
                string chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe");
                if (!File.Exists(chromePath))
                {
                    chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe");
                }

                if (File.Exists(chromePath))
                {
                    log("Abriendo en Google Chrome modo App standalone.");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = chromePath,
                        Arguments = "--app=\"" + AppUrl + "\"",
                        UseShellExecute = true
                    });
                    return;
                }

                // 2. Probar Microsoft Edge en modo aplicación (--app)
                string edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
                if (!File.Exists(edgePath))
                {
                    edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe");
                }

                if (File.Exists(edgePath))
                {
                    log("Abriendo en Microsoft Edge modo App standalone.");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = edgePath,
                        Arguments = "--app=\"" + AppUrl + "\"",
                        UseShellExecute = true
                    });
                    return;
                }
            }

            // Fallback a navegador predeterminado de Windows
            log("Abriendo en el navegador predeterminado de Windows.");
            Process.Start(new ProcessStartInfo
            {
                FileName = AppUrl,
                UseShellExecute = true
            });
        }
    }
}
