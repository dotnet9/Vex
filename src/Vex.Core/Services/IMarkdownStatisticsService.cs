using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IMarkdownStatisticsService
{
    MarkdownStatistics Count(string markdown);
}
