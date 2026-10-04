using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class BreadcrumbBufferTests
{
    [Fact]
    public void Add_StoresBreadcrumbs()
    {
        var buffer = new BreadcrumbBuffer();

        buffer.Add(
            new TraceXBreadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Type = "test",
                Message = "first"
            });

        var breadcrumbs = buffer.GetAll();

        Assert.Single(breadcrumbs);
        Assert.Equal("test", breadcrumbs[0].Type);
        Assert.Equal("first", breadcrumbs[0].Message);
    }

    [Fact]
    public void Add_KeepsMaximum50Breadcrumbs()
    {
        var buffer = new BreadcrumbBuffer();

        for (var i = 1; i <= 60; i++)
        {
            buffer.Add(
                new TraceXBreadcrumb
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "test",
                    Message = $"breadcrumb-{i}"
                });
        }

        var breadcrumbs = buffer.GetAll();

        Assert.Equal(50, breadcrumbs.Count);
        Assert.Equal("breadcrumb-11", breadcrumbs[0].Message);
        Assert.Equal("breadcrumb-60", breadcrumbs[^1].Message);
    }

    [Fact]
    public void Add_RemovesOldestBreadcrumbFirst()
    {
        var buffer = new BreadcrumbBuffer();

        for (var i = 1; i <= 51; i++)
        {
            buffer.Add(
                new TraceXBreadcrumb
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "test",
                    Message = $"breadcrumb-{i}"
                });
        }

        var breadcrumbs = buffer.GetAll();

        Assert.Equal(50, breadcrumbs.Count);
        Assert.DoesNotContain(
            breadcrumbs,
            breadcrumb => breadcrumb.Message == "breadcrumb-1");

        Assert.Equal(
            "breadcrumb-2",
            breadcrumbs[0].Message);
    }

    [Fact]
    public void Count_ReturnsCurrentNumberOfBreadcrumbs()
    {
        var buffer = new BreadcrumbBuffer();

        Assert.Equal(0, buffer.Count);

        buffer.Add(
            new TraceXBreadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Type = "test",
                Message = "one"
            });

        buffer.Add(
            new TraceXBreadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Type = "test",
                Message = "two"
            });

        Assert.Equal(2, buffer.Count);
    }
}