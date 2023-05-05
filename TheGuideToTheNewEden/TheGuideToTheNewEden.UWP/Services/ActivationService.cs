﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using TheGuideToTheNewEden.UWP.Activation;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.UWP.Services;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TheGuideToTheNewEden.UWP.Services
{
    // For more information on understanding and extending activation flow see
    // https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/activation.md
    internal class ActivationService
    {
        private readonly App _app;
        private readonly Type _defaultNavItem;
        private Lazy<UIElement> _shell;

        private object _lastActivationArgs;

        public ActivationService(App app, Type defaultNavItem, Lazy<UIElement> shell = null)
        {
            _app = app;
            _shell = shell;
            _defaultNavItem = defaultNavItem;
        }
        public static Enums.ProtocolType ProtocolType = Enums.ProtocolType.CharacterOauth;
        public async Task ActivateAsync(object activationArgs)
        {
            if(activationArgs is ProtocolActivatedEventArgs )
            {
                HandleProtocol(activationArgs as ProtocolActivatedEventArgs);
            }
            else
            {
                if (IsInteractive(activationArgs))
                {
                    // Initialize services that you need before app activation
                    // take into account that the splash screen is shown while this code runs.
                    await InitializeAsync();

                    // Do not repeat app initialization when the Window already has content,
                    // just ensure that the window is active
                    if (Window.Current.Content == null)
                    {
                        // Create a Shell or Frame to act as the navigation context
                        Window.Current.Content = _shell?.Value ?? new Frame();
                    }
                }

                // Depending on activationArgs one of ActivationHandlers or DefaultActivationHandler
                // will navigate to the first page
                await HandleActivationAsync(activationArgs);
                _lastActivationArgs = activationArgs;

                if (IsInteractive(activationArgs))
                {
                    // Ensure the current window is active
                    Window.Current.Activate();

                    // Tasks after activation
                    await StartupAsync();
                }
            }
        }

        private async Task InitializeAsync()
        {
            await Singleton<BackgroundTaskService>.Instance.RegisterBackgroundTasksAsync().ConfigureAwait(false);
            await ThemeSelectorService.InitializeAsync().ConfigureAwait(false);
            await LanguageSelectorService.InitializeAsync().ConfigureAwait(false);
            await DBLanguageSelectorService.InitializeAsync().ConfigureAwait(false);
            await GameServerSelectorService.InitializeAsync().ConfigureAwait(false);
            await PlayerStatusService.InitializeAsync().ConfigureAwait(false);
            await CharacterService.InitAsync().ConfigureAwait(false);
            CoreConfig.DBPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = LanguageSelectorService.Language;
        }

        private async Task HandleActivationAsync(object activationArgs)
        {
            var activationHandler = GetActivationHandlers()
                                                .FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsInteractive(activationArgs))
            {
                var defaultHandler = new DefaultActivationHandler(_defaultNavItem);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }
            }
        }

        private async Task StartupAsync()
        {
            await ThemeSelectorService.SetRequestedThemeAsync();
            await Task.CompletedTask;
        }

        private IEnumerable<ActivationHandler> GetActivationHandlers()
        {
            yield return Singleton<BackgroundTaskService>.Instance;
            yield return Singleton<ToastNotificationsService>.Instance;
        }

        private bool IsInteractive(object args)
        {
            return args is IActivatedEventArgs;
        }

        private void HandleProtocol(ProtocolActivatedEventArgs args)
        {
            switch (ProtocolType)
            {
                case Enums.ProtocolType.CharacterOauth: CharacterService.HandelCharacterOatuhProtocol(args.Uri.ToString());break;
                case Enums.ProtocolType.StructureOauth: CharacterService.HandelStructureOauthProtocol(args.Uri.ToString()); break;
            }
        }
    }
}