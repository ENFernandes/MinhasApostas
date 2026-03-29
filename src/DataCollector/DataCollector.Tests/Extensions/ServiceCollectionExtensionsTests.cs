using Microsoft.Extensions.DependencyInjection;
using SportsBetting.DataCollector.Api.Extensions;
using SportsBetting.DataCollector.Core.Interfaces;
using Xunit;

namespace SportsBetting.DataCollector.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private interface ITestService
    {
        void DoSomething();
    }

    private class TestScopedService : ITestService, IScopedService
    {
        public void DoSomething() { }
    }

    private class TestTransientService : ITestService, ITransientService
    {
        public void DoSomething() { }
    }

    private class TestSingletonService : ITestService, ISingletonService
    {
        public void DoSomething() { }
    }

    [Fact]
    public void AddServicesByReflection_ShouldRegisterScopedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServicesByReflection(typeof(TestScopedService).Assembly);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ITestService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(TestScopedService), descriptor.ImplementationType);
    }

    [Fact]
    public void AddServicesByReflection_ShouldRegisterTransientService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServicesByReflection(typeof(TestTransientService).Assembly);

        // Assert
        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(ITestService) &&
            s.ImplementationType == typeof(TestTransientService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddServicesByReflection_ShouldRegisterSingletonService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServicesByReflection(typeof(TestSingletonService).Assembly);

        // Assert
        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(ITestService) &&
            s.ImplementationType == typeof(TestSingletonService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddServicesByReflection_ShouldRegisterMultipleInterfaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServicesByReflection(typeof(TestScopedService).Assembly);

        // Assert
        var scopedDescriptors = services.Where(s => s.ImplementationType == typeof(TestScopedService));
        Assert.Single(scopedDescriptors);
    }
}
