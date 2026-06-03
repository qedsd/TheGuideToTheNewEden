using Microsoft.UI.Xaml;
using System;
using TheGuideToTheNewEden.Core;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    /// <summary>
    /// 管理单个预览窗口的 DWM 缩略图注册、健康检查与自动重建。
    /// </summary>
    internal sealed class GamePreviewDwmThumbnailHost
    {
        private const int MaxConsecutiveUpdateFailures = 3;
        private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan SourceStateCheckInterval = TimeSpan.FromSeconds(1);

        private IntPtr _thumbHwnd = IntPtr.Zero;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private IntPtr _sourceHwnd = IntPtr.Zero;
        private Window _ownerWindow;
        private DispatcherTimer _healthCheckTimer;
        private DispatcherTimer _sourceStateTimer;
        private int _consecutiveFailures;
        private bool _sourcePaused;
        private bool _lastSourceIconic;
        private Action _onThumbnailRebuilt;

        public IntPtr ThumbHandle => _thumbHwnd;
        public bool IsActive => _thumbHwnd != IntPtr.Zero;

        public void Start(Window ownerWindow, IntPtr targetHwnd, IntPtr sourceHwnd, Action onThumbnailRebuilt)
        {
            Stop();
            _ownerWindow = ownerWindow;
            _targetHwnd = targetHwnd;
            _sourceHwnd = sourceHwnd;
            _onThumbnailRebuilt = onThumbnailRebuilt;
            _consecutiveFailures = 0;
            _sourcePaused = false;
            _lastSourceIconic = WindowCaptureHelper.IsSourceWindowIconic(_sourceHwnd);
            _thumbHwnd = WindowCaptureHelper.RebuildThumbnail(_targetHwnd, IntPtr.Zero, _sourceHwnd);
            if (_thumbHwnd == IntPtr.Zero)
            {
                Log.Error($"GamePreview DWM thumbnail register failed. target={_targetHwnd}, source={_sourceHwnd}");
            }
            StartHealthCheck();
            StartSourceStateMonitor();
        }

        public void UpdateSourceHwnd(IntPtr sourceHwnd)
        {
            if (_sourceHwnd == sourceHwnd)
            {
                return;
            }
            _sourceHwnd = sourceHwnd;
            _lastSourceIconic = WindowCaptureHelper.IsSourceWindowIconic(_sourceHwnd);
            _sourcePaused = !WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd);
            if (_thumbHwnd != IntPtr.Zero && WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                Rebuild(forceRefresh: true);
            }
        }

        public bool TryUpdate(WindowCaptureHelper.Rect rcDestination, WindowCaptureHelper.Rect rcSource)
        {
            if (!WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                PauseForUnavailableSource();
                return false;
            }
            if (_sourcePaused)
            {
                _sourcePaused = false;
                if (!Rebuild(forceRefresh: true))
                {
                    return false;
                }
            }
            else if (_thumbHwnd == IntPtr.Zero || !WindowCaptureHelper.TryQueryThumbnailSourceSize(_thumbHwnd, out _))
            {
                if (!Rebuild(forceRefresh: false))
                {
                    return false;
                }
            }
            if (WindowCaptureHelper.TryUpdateThumbDestination(_thumbHwnd, rcDestination, rcSource))
            {
                _consecutiveFailures = 0;
                return true;
            }
            _consecutiveFailures++;
            Log.Error($"GamePreview DWM update failed ({_consecutiveFailures}/{MaxConsecutiveUpdateFailures}). target={_targetHwnd}, source={_sourceHwnd}");
            if (_consecutiveFailures >= MaxConsecutiveUpdateFailures)
            {
                if (!Rebuild(forceRefresh: false))
                {
                    return false;
                }
                if (WindowCaptureHelper.TryUpdateThumbDestination(_thumbHwnd, rcDestination, rcSource))
                {
                    _consecutiveFailures = 0;
                    return true;
                }
                return false;
            }
            return false;
        }

        public void RecoverAfterSourceRestore()
        {
            if (_ownerWindow == null)
            {
                return;
            }
            if (!_ownerWindow.DispatcherQueue.HasThreadAccess)
            {
                _ownerWindow.DispatcherQueue.TryEnqueue(RecoverAfterSourceRestore);
                return;
            }
            if (!WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                return;
            }
            _sourcePaused = false;
            _lastSourceIconic = false;
            if (_thumbHwnd == IntPtr.Zero || !WindowCaptureHelper.TryQueryThumbnailSourceSize(_thumbHwnd, out _))
            {
                Rebuild(forceRefresh: true);
            }
            else
            {
                WindowCaptureHelper.TrySetThumbnailVisible(_thumbHwnd, true);
                _onThumbnailRebuilt?.Invoke();
            }
        }

        public void Stop()
        {
            StopHealthCheck();
            StopSourceStateMonitor();
            if (_thumbHwnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHwnd);
                _thumbHwnd = IntPtr.Zero;
            }
            _ownerWindow = null;
            _onThumbnailRebuilt = null;
            _consecutiveFailures = 0;
            _sourcePaused = false;
            _lastSourceIconic = false;
        }

        private void PauseForUnavailableSource()
        {
            _sourcePaused = true;
            if (_thumbHwnd != IntPtr.Zero)
            {
                WindowCaptureHelper.TrySetThumbnailVisible(_thumbHwnd, false);
            }
        }

        private bool Rebuild(bool forceRefresh)
        {
            _consecutiveFailures = 0;
            var previousThumb = _thumbHwnd;
            var newThumb = WindowCaptureHelper.RebuildThumbnail(_targetHwnd, _thumbHwnd, _sourceHwnd);
            if (newThumb == IntPtr.Zero)
            {
                _thumbHwnd = IntPtr.Zero;
                Log.Error($"GamePreview DWM thumbnail rebuild failed. target={_targetHwnd}, source={_sourceHwnd}");
                return false;
            }
            var rebuilt = newThumb != previousThumb;
            _thumbHwnd = newThumb;
            if (rebuilt)
            {
                Log.Info($"GamePreview DWM thumbnail rebuilt. target={_targetHwnd}, source={_sourceHwnd}");
            }
            if (rebuilt || forceRefresh)
            {
                _onThumbnailRebuilt?.Invoke();
            }
            return true;
        }

        private void StartHealthCheck()
        {
            if (_ownerWindow == null)
            {
                return;
            }
            if (_healthCheckTimer == null)
            {
                _healthCheckTimer = new DispatcherTimer()
                {
                    Interval = HealthCheckInterval,
                };
                _healthCheckTimer.Tick += HealthCheckTimer_Tick;
            }
            _healthCheckTimer.Start();
        }

        private void StopHealthCheck()
        {
            _healthCheckTimer?.Stop();
        }

        private void StartSourceStateMonitor()
        {
            if (_ownerWindow == null)
            {
                return;
            }
            if (_sourceStateTimer == null)
            {
                _sourceStateTimer = new DispatcherTimer()
                {
                    Interval = SourceStateCheckInterval,
                };
                _sourceStateTimer.Tick += SourceStateTimer_Tick;
            }
            _sourceStateTimer.Start();
        }

        private void StopSourceStateMonitor()
        {
            _sourceStateTimer?.Stop();
        }

        private void HealthCheckTimer_Tick(object sender, object e)
        {
            if (_ownerWindow == null)
            {
                return;
            }
            if (!WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                PauseForUnavailableSource();
                return;
            }
            if (_thumbHwnd == IntPtr.Zero || _sourcePaused || !WindowCaptureHelper.IsThumbnailHealthy(_thumbHwnd, _sourceHwnd))
            {
                Log.Info($"GamePreview DWM health check failed, rebuilding thumbnail. target={_targetHwnd}, source={_sourceHwnd}");
                RecoverAfterSourceRestore();
            }
        }

        private void SourceStateTimer_Tick(object sender, object e)
        {
            if (_ownerWindow == null || _sourceHwnd == IntPtr.Zero)
            {
                return;
            }
            var sourceIconic = WindowCaptureHelper.IsSourceWindowIconic(_sourceHwnd);
            if (_lastSourceIconic && !sourceIconic && WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                Log.Info($"GamePreview source window restored from minimize. source={_sourceHwnd}");
                RecoverAfterSourceRestore();
            }
            else if (!sourceIconic && _sourcePaused && WindowCaptureHelper.IsSourceWindowValid(_sourceHwnd))
            {
                RecoverAfterSourceRestore();
            }
            else if (sourceIconic)
            {
                PauseForUnavailableSource();
            }
            _lastSourceIconic = sourceIconic;
        }
    }
}
