﻿// based on https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Spa/SpaProxy/src/SpaProxyLaunchManager.cs

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace AspNetCore.SpaYarp;

public class SpaProxyLaunchManager<T> : IDisposable where T : SpaDevelopmentServerOptions
{
    private readonly T _options;
    private readonly ILogger<SpaProxyLaunchManager<T>> _logger;
    private readonly object _lock = new object();

    private Process? _spaProcess;
    private bool _disposedValue;
    private Task? _launchTask;

    public SpaProxyLaunchManager(
        ILogger<SpaProxyLaunchManager<T>> logger,
        IHostApplicationLifetime appLifetime,
        IOptions<T> options)
    {
        _options = options.Value;
        _logger = logger;
        appLifetime.ApplicationStopping.Register(() => Dispose(true));
    }

    public void StartInBackground(CancellationToken cancellationToken)
    {
        // We are not waiting for the SPA proxy to launch, instead we are going to rely on a piece of
        // middleware to display an HTML document while the SPA proxy is not ready, refresh every three
        // seconds and redirect to the SPA proxy url once it is ready.
        // Being ready in this context means that we were able to receive a 200 from the proxy or that
        // we gave up waiting.
        // We do this to ensure Visual Studio can work correctly with IIS and when running without debugging.
        lock (_lock)
        {
            if (_launchTask == null)
            {
                _logger.LogInformation($"No SPA development server running at {_options.ClientUrl} found.");
                _launchTask = UpdateStatus(StartSpaProcessAndProbeForLiveness(cancellationToken));
            }
        }

        async Task UpdateStatus(Task launchTask)
        {
            try
            {
                await launchTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error trying to launch the SPA client.");
            }
            finally
            {
                lock (_lock)
                {
                    _launchTask = null;
                }
            }
        }
    }

    public async Task<bool> IsSpaClientRunning(CancellationToken cancellationToken)
    {
        var httpClient = CreateHttpClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            var url = _options.ClientUrl;
            if (!string.IsNullOrEmpty(_options.PublicPath))
            {
                var uri = new Uri(new Uri(url), _options.PublicPath);
                url = uri.ToString();
            }
            var response = await httpClient.GetAsync(url, cancellationTokenSource.Token);
            var running = response.IsSuccessStatusCode;
            return running;
        }
        catch (Exception exception) when (exception is HttpRequestException ||
              exception is TaskCanceledException ||
              exception is OperationCanceledException)
        {
            _logger.LogDebug(exception, "Failed to connect to the SPA Development proxy.");
            return false;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient(new HttpClientHandler()
        {
            UseProxy = false,
            // It's ok for us to do this here since this service is only plugged in during development.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        // We don't care about the returned content type as long as the server is able to answer with 2XX
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.1));
        return httpClient;
    }

    private async Task<bool> ProbeSpaDevelopmentServerUrl(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            var response = await httpClient.GetAsync(_options.ClientUrl, cancellationTokenSource.Token);
            var running = response.IsSuccessStatusCode;
            return running;
        }
        catch (Exception exception) when (exception is HttpRequestException ||
              exception is TaskCanceledException ||
              exception is OperationCanceledException)
        {
            _logger.LogDebug(exception, "Failed to connect to the SPA Development proxy.");
            return false;
        }
    }

    private async Task StartSpaProcessAndProbeForLiveness(CancellationToken cancellationToken)
    {
        LaunchDevelopmentClient();
        var sw = Stopwatch.StartNew();
        var livenessProbeSucceeded = false;
        var maxTimeoutReached = false;
        var httpClient = CreateHttpClient();
        while (_spaProcess != null && !_spaProcess.HasExited && !maxTimeoutReached)
        {
            livenessProbeSucceeded = await ProbeSpaDevelopmentServerUrl(httpClient, cancellationToken);
            if (livenessProbeSucceeded)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            maxTimeoutReached = sw.Elapsed >= _options.MaxTimeout;
            await Task.Delay(1000, cancellationToken);
        }

        if (_spaProcess == null || _spaProcess.HasExited)
        {
            _logger.LogError($"Couldn't start the SPA development server with command '{_options.LaunchCommand}'.");
        }
        else if (!livenessProbeSucceeded)
        {
            _logger.LogError($"Unable to connect to the SPA development server at '{_options.ClientUrl}'.");
        }
        else
        {
            _logger.LogInformation($"SPA development server running at '{_options.ClientUrl}'");
        }
    }

    private void LaunchDevelopmentClient()
    {
        try
        {
            // Launch command is going to be something like `npm/yarn <<verb>> <<options>>`
            // We split it into two to separate the tool (command) from the verb and the rest of the arguments.
            var space = _options.LaunchCommand.IndexOf(' ');
            var command = _options.LaunchCommand[0..space];
            var arguments = _options.LaunchCommand[++space..];
#if NETCOREAPP3_1
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) && !Path.HasExtension(command))
#else
            if (OperatingSystem.IsWindows() && !Path.HasExtension(command))
#endif
            {
                // On windows we transform npm/yarn to npm.cmd/yarn.cmd so that the command
                // can actually be found when we start the process. This is overridable if
                // necessary by explicitly setting up the extension on the command.
                command = $"{command}.cmd";
            }

            var info = new ProcessStartInfo(command, arguments)
            {
                // Linux and Mac OS don't have the concept of launching a terminal process in a new window.
                // On those cases the process will be launched in the same terminal window and will just print
                // some output during the start phase of the app.
                // This is not a problem since users don't need to interact with the proxy other than to stop it
                // and this is only an optimization to keep the current experience. We can always tell them to
                // run the proxy manually.
                CreateNoWindow = false,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Path.Combine(AppContext.BaseDirectory, _options.WorkingDirectory)
            };
            _spaProcess = Process.Start(info);
            if (_spaProcess != null && !_spaProcess.HasExited)
            {
#if NETCOREAPP3_1
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
#else
                if (OperatingSystem.IsWindows())
#endif
                {
                    LaunchStopScriptWindows(_spaProcess.Id);
                }
#if NETCOREAPP3_1
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
#else
                else if (OperatingSystem.IsMacOS())
#endif
                {
                    LaunchStopScriptMacOS(_spaProcess.Id);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Failed to launch the SPA development server '{_options.LaunchCommand}'.");
        }
    }

    private void LaunchStopScriptWindows(int spaProcessId)
    {
#if NETCOREAPP3_1
        var processId = Process.GetCurrentProcess().Id;
#else
        var processId = Environment.ProcessId;
#endif
        var stopScript = $@"do{{
  try
  {{
    $processId = Get-Process -PID {processId} -ErrorAction Stop;
  }}catch
  {{
    $processId = $null;
  }}
  Start-Sleep -Seconds 1;
}}while($processId -ne $null);
try
{{
  taskkill /T /F /PID {spaProcessId};
}}
catch
{{
}}";
        var stopScriptInfo = new ProcessStartInfo(
            "powershell.exe",
            string.Join(" ", "-NoProfile", "-C", stopScript))
        {
            CreateNoWindow = true,
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, _options.WorkingDirectory)
        };

        var stopProcess = Process.Start(stopScriptInfo);
        if (stopProcess == null || stopProcess.HasExited)
        {
            _logger.LogWarning($"The SPA process shutdown script '{stopProcess?.Id}' failed to start. The SPA proxy might" +
                $" remain open if the dotnet process is terminated ungracefully. Use the operating system commands to kill" +
                $" the process tree for {spaProcessId}");
        }
        else
        {
            _logger.LogDebug($"Watch process '{stopProcess}' started.");
        }
    }

    private void LaunchStopScriptMacOS(int spaProcessId)
    {
#if NETCOREAPP3_1
        var processId = Process.GetCurrentProcess().Id;
#else
        var processId = Environment.ProcessId;
#endif
        var fileName = Guid.NewGuid().ToString("N") + ".sh";
        var scriptPath = Path.Combine(AppContext.BaseDirectory, fileName);
        var stopScript = @$"function list_child_processes(){{
    local ppid=$1;
    local current_children=$(pgrep -P $ppid);
    local local_child;
    if [ $? -eq 0 ];
    then
        for current_child in $current_children
        do
          local_child=$current_child;
          list_child_processes $local_child;
          echo $local_child;
        done;
    else
      return 0;
    fi;
}}
ps {processId};
while [ $? -eq 0 ];
do
  sleep 1;
  ps {processId} > /dev/null;
done;
for child in $(list_child_processes {spaProcessId});
do
  echo killing $child;
  kill -s KILL $child;
done;
rm {scriptPath};
";
        File.WriteAllText(scriptPath, stopScript);

        var stopScriptInfo = new ProcessStartInfo("/bin/bash", scriptPath)
        {
            CreateNoWindow = true,
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, _options.WorkingDirectory)
        };

        var stopProcess = Process.Start(stopScriptInfo);
        if (stopProcess == null || stopProcess.HasExited)
        {
            _logger.LogWarning($"The SPA process shutdown script '{stopProcess?.Id}' failed to start. The SPA proxy might" +
                $" remain open if the dotnet process is terminated ungracefully. Use the operating system commands to kill" +
                $" the process tree for {spaProcessId}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose(true);
        return Task.CompletedTask;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Nothing to do here since ther are no managed resources
            }

            try
            {
                if (_spaProcess != null && !_spaProcess.HasExited)
                {
                    // Review: Whether or not to do this at all. Turns out that if we try to kill the
                    // npm.cmd/ps1 process that we start, even with this option we only stop this process
                    // and the service keeps running.
                    // Compared to performing Ctrl+C on the window or closing the window for the newly spawned
                    // process which seems to do the right thing.
                    // Process.CloseMainWindow seems to do the right thing in this situation and is doable since
                    // we now start a proxy every time.
                    // We can't guarantee that we stop/cleanup the proxy on every situation (for example if someone)
                    // kills this process in a "rude" way, but this gets 95% there.
                    // For cases where the proxy is left open and where there might not be a "visible" window the recomendation
                    // is to kill the process manually. (We will not fail, we will simply notify the proxy is "already" up.
                    if (!_spaProcess.CloseMainWindow())
                    {
                        _spaProcess.Kill(entireProcessTree: true);
                        _spaProcess = null;
                    }
                }
            }
            catch (Exception)
            {
                // Avoid throwing if we are running inside the finalizer.
                if (disposing)
                {
                    throw;
                }
            }

            _disposedValue = true;
        }
    }

    ~SpaProxyLaunchManager()
    {
        Dispose(disposing: false);
    }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}