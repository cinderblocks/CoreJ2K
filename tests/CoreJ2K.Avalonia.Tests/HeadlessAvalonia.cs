// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace CoreJ2K.Avalonia.Tests
{
    /// <summary>
    /// Initializes a headless Avalonia application exactly once for the entire xUnit
    /// test assembly. We run a UI thread in the background so any test that touches
    /// platform-bound APIs (e.g. <see cref="Avalonia.Media.Imaging.WriteableBitmap"/>)
    /// can dispatch work onto it.
    /// </summary>
    public static class HeadlessAvalonia
    {
        private static readonly object _gate = new();
        private static bool _started;
        private static Thread? _uiThread;
        private static ManualResetEventSlim? _ready;

        public static void EnsureStarted()
        {
            if (_started) return;
            lock (_gate)
            {
                if (_started) return;

                _ready = new ManualResetEventSlim(false);
                _uiThread = new Thread(UiThreadEntry)
                {
                    Name = "Avalonia Headless UI",
                    IsBackground = true
                };
                _uiThread.Start();
                _ready.Wait();
                _started = true;
            }
        }

        private static void UiThreadEntry()
        {
            var builder = AppBuilder.Configure<TestApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .UseSkia();

            builder.SetupWithoutStarting();

            _ready!.Set();
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        }

        public static T Invoke<T>(Func<T> func)
        {
            EnsureStarted();
            return Dispatcher.UIThread.InvokeAsync(func).GetTask().GetAwaiter().GetResult();
        }
    }

    public sealed class TestApp : Application
    {
    }
}
