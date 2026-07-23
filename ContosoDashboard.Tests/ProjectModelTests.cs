using ContosoDashboard.Models;
using TaskStatus = ContosoDashboard.Models.TaskStatus;

namespace ContosoDashboard.Tests;

public class ProjectModelTests
{
    [Fact]
    public void CompletionPercentage_ReturnsZero_WhenNoTasks()
    {
        var project = new Project { Name = "P" };

        Assert.Equal(0, project.CompletionPercentage);
    }

    [Fact]
    public void CompletionPercentage_ReturnsZero_WhenNoTasksCompleted()
    {
        var project = new Project
        {
            Name = "P",
            Tasks = new List<TaskItem>
            {
                new TaskItem { Title = "a", Status = TaskStatus.NotStarted },
                new TaskItem { Title = "b", Status = TaskStatus.InProgress }
            }
        };

        Assert.Equal(0, project.CompletionPercentage);
    }

    [Fact]
    public void CompletionPercentage_Returns100_WhenAllTasksCompleted()
    {
        var project = new Project
        {
            Name = "P",
            Tasks = new List<TaskItem>
            {
                new TaskItem { Title = "a", Status = TaskStatus.Completed },
                new TaskItem { Title = "b", Status = TaskStatus.Completed }
            }
        };

        Assert.Equal(100, project.CompletionPercentage);
    }

    [Theory]
    [InlineData(1, 4, 25)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 66)]
    public void CompletionPercentage_RoundsDown_ForPartialCompletion(int completed, int total, int expected)
    {
        var tasks = new List<TaskItem>();
        for (int i = 0; i < total; i++)
            tasks.Add(new TaskItem { Title = $"t{i}", Status = i < completed ? TaskStatus.Completed : TaskStatus.NotStarted });

        var project = new Project { Name = "P", Tasks = tasks };

        Assert.Equal(expected, project.CompletionPercentage);
    }
}
