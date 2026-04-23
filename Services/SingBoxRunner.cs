using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ProxyShellReady.Services
{
    public class SingBoxRunner
    {
        private Process _process;
        private string _currentConfigPath = string.Empty;

        public bool IsRunning
        {
            get { return _process != null && !_process.HasExited; }
        }

        public string CurrentConfigPath
        {
            get { return _currentConfigPath; }
        }

        public async Task<string> StartAsync(string singBoxExePath, string configJson, Action<string> onLog)
        {
            if (IsRunning)
                throw new InvalidOperationException("sing-box уже запущен.");

            if (!File.Exists(singBoxExePath))
                throw new FileNotFoundException("Не найден sing-box.exe", singBoxExePath);

            string runtimeDirectory = StateStore.EnsureRuntimeDirectory();
            _currentConfigPath = Path.Combine(runtimeDirectory, "generated-config.json");
            await File.WriteAllTextAsync(_currentConfigPath, configJson, new UTF8Encoding(false));

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = singBoxExePath;
            startInfo.Arguments = "run -c \"" + _currentConfigPath + "\"";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = Path.GetDirectoryName(singBoxExePath) ?? AppContext.BaseDirectory;

            _process = new Process();
            _process.StartInfo = startInfo;
            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    onLog("[OUT] " + e.Data);
            };
            _process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    onLog("[ERR] " + e.Data);
            };
            _process.Exited += delegate
            {
                onLog("Процесс sing-box завершен.");
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            return _currentConfigPath;
        }

        public void Stop(Action<string> onLog)
        {
            try
            {
                if (!IsRunning)
                    return;

                _process.Kill(true);
                _process.WaitForExit(3000);
                onLog("Остановка sing-box завершена.");
            }
            catch (Exception ex)
            {
                onLog("Ошибка остановки: " + ex.Message);
            }
            finally
            {
                if (_process != null)
                    _process.Dispose();
                _process = null;
            }
        }
    }
}
