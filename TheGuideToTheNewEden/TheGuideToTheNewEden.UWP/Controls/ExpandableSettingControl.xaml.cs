﻿using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace TheGuideToTheNewEden.UWP.Controls
{
    [ContentProperty(Name = nameof(SettingActionableElement))]
    public sealed partial class ExpandableSettingControl : UserControl
    {
        public FrameworkElement SettingActionableElement { get; set; }

        public static readonly DependencyProperty TitleProperty
            = DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ExpandableSettingControl),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty
            = DependencyProperty.Register(
                nameof(Description),
                typeof(string),
                typeof(ExpandableSettingControl),
                new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty IconProperty
            = DependencyProperty.Register(
                nameof(Icon),
                typeof(IconElement),
                typeof(ExpandableSettingControl),
                new PropertyMetadata(null));

        public IconElement Icon
        {
            get => (IconElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty ExpandableContentProperty
            = DependencyProperty.Register(
                nameof(ExpandableContent),
                typeof(FrameworkElement),
                typeof(ExpandableSettingControl),
                new PropertyMetadata(null));

        public FrameworkElement ExpandableContent
        {
            get => (FrameworkElement)GetValue(ExpandableContentProperty);
            set => SetValue(ExpandableContentProperty, value);
        }

        public static readonly DependencyProperty IsExpandedProperty
            = DependencyProperty.Register(
                nameof(IsExpanded),
                typeof(bool),
                typeof(ExpandableSettingControl),
                new PropertyMetadata(false));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public ExpandableSettingControl()
        {
            InitializeComponent();
        }

        private void Expander_Loaded(object sender, RoutedEventArgs e)
        {
            AutomationProperties.SetName(Expander, Title);
            AutomationProperties.SetHelpText(Expander, Description);
        }
    }
}
