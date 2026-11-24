using System.Globalization;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Xunit;

namespace Vex.Core.Tests;

public sealed class ShellFindBarAccessibilityTests
{
    [Fact]
    public void InvalidRegexShowsActionableErrorAndSuppressesCountCommands()
    {
        var observer = new SearchObserver();
        var status = new StatusObserver();
        var viewModel = new ShellFindBarViewModel(new TestLocalizer(), status);
        EventBus.Default.Subscribe(observer);
        try
        {
            viewModel.IsVisible = true;
            viewModel.IsRegex = true;
            viewModel.SearchText = "[";

            Assert.True(viewModel.HasSearchError);
            Assert.Contains("Invalid regular expression:", viewModel.SearchError);
            Assert.False(viewModel.ShowFindCount);

            viewModel.FindNext();
            Assert.Empty(observer.Commands);
            Assert.Contains(viewModel.SearchError, status.Message);

            viewModel.IsRegex = false;
            Assert.False(viewModel.HasSearchError);
            Assert.True(viewModel.ShowFindCount);
            Assert.Empty(viewModel.SearchError);
        }
        finally
        {
            EventBus.Default.Unsubscribe(observer);
            EventBus.Default.Unsubscribe(viewModel);
        }
    }

    [Fact]
    public void MatchCaseChangeRevalidatesTheExpression()
    {
        var viewModel = new ShellFindBarViewModel(new TestLocalizer(), new StatusObserver());
        try
        {
            viewModel.IsVisible = true;
            viewModel.IsRegex = true;
            viewModel.SearchText = "(?-i:ok)";
            Assert.False(viewModel.HasSearchError);

            viewModel.IsMatchCase = true;
            Assert.False(viewModel.HasSearchError);
        }
        finally { EventBus.Default.Unsubscribe(viewModel); }
    }

    private sealed class SearchObserver
    {
        public List<EditorSearchCommand> Commands { get; } = [];

        [EventHandler]
        public void Handle(EditorSearchCommand command) => Commands.Add(command);
    }

    private sealed class StatusObserver : IShellStatusPublisher
    {
        public string Message { get; private set; } = string.Empty;
        public void Publish(string message) => Message = message;
        public void PublishResource(string key) => Message = key;
        public void PublishResourceFormat(string key, params object?[] args) => Message = string.Format(CultureInfo.InvariantCulture, key, args);
    }

    private sealed class TestLocalizer : IAppLocalizer
    {
        public event EventHandler<EventArgs>? CultureChanged { add { } remove { } }
        public CultureInfo Culture => CultureInfo.InvariantCulture;
        public void SetCulture(string cultureName) { }
        public string Get(string key) => key == VexL.FindRegexInvalid ? "Invalid regular expression: {0}" : key;
        public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    }
}
