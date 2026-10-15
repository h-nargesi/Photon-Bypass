using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Management;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.Initializer;
using Quartz;
using System.Collections.Concurrent;
using System.Reflection;
using Mock = Moq.Mock;

namespace PhotonBypass.Test.Facts.Application;

public class JobIntervalTest : UnitLevelServiceInitializer
{
    private readonly Mock<IServerManagementService> _serverManagementMoq = new();
    private readonly Mock<IAccountMonitoringService> _accountMonitoringMoq = new();
    private readonly Mock<IAccountRadiusSyncService> _accountRadiusMoq = new();
    private readonly Mock<IWalletThresholdService> _walletThresholdMoq = new();
    private readonly Mock<IPlanStateRepository> _planStateRepoMoq = new();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient(_ => _serverManagementMoq.Object);
        builder.Services.AddTransient(_ => _accountMonitoringMoq.Object);
        builder.Services.AddTransient(_ => _accountRadiusMoq.Object);
        builder.Services.AddTransient(_ => _walletThresholdMoq.Object);
        builder.Services.AddTransient(_ => _planStateRepoMoq.Object);
    }

    [Fact]
    public async Task Execute_WithoutPlanStates_RunsOnlyTrafficUpdateAndWalletRule()
    {
        _planStateRepoMoq.Setup(repo => repo.GetAll()).ReturnsAsync([]);
        _serverManagementMoq.Setup(srv => srv.UpdateTrafficData()).Returns(Task.CompletedTask);
        _walletThresholdMoq.Setup(srv => srv.CheckAndApply()).Returns(Task.CompletedTask);

        var job = new JobInterval(App.Services);
        await job.Execute(Moq.Mock.Of<IJobExecutionContext>());

        _serverManagementMoq.Verify(srv => srv.UpdateTrafficData(), Times.Once);
        _walletThresholdMoq.Verify(srv => srv.CheckAndApply(), Times.Once);

        // the parallel branch must never be entered with an empty plan-state list
        _accountMonitoringMoq.Verify(srv => srv.NotifSendServices(It.IsAny<IEnumerable<PlanStateEntity>>()), Times.Never);
        _accountMonitoringMoq.Verify(srv => srv.InactiveAbandonedUsers(It.IsAny<IEnumerable<PlanStateEntity>>()), Times.Never);
        _accountRadiusMoq.Verify(srv => srv.DeactivateInvalidRadiusUsers(It.IsAny<IEnumerable<PlanStateEntity>>()), Times.Never);
        _serverManagementMoq.Verify(srv => srv.CheckUserServerBalance(), Times.Never);
    }

    [Fact]
    public async Task Execute_WithPlanStates_UpdatesTraffic_ThenRunsAllParallelJobs_ThenWalletRule()
    {
        var plan_states = new List<PlanStateEntity> { new() };
        _planStateRepoMoq.Setup(repo => repo.GetAll()).ReturnsAsync(plan_states);

        var order = new ConcurrentQueue<string>();
        var parallel_gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _serverManagementMoq.Setup(srv => srv.UpdateTrafficData())
            .Callback(() => order.Enqueue("update-traffic"))
            .Returns(Task.CompletedTask);

        _accountMonitoringMoq.Setup(srv => srv.NotifSendServices(plan_states))
            .Callback(() => order.Enqueue("notif"))
            .Returns(parallel_gate.Task);
        _accountRadiusMoq.Setup(srv => srv.DeactivateInvalidRadiusUsers(plan_states))
            .Callback(() => order.Enqueue("deactivate"))
            .Returns(parallel_gate.Task);
        _accountMonitoringMoq.Setup(srv => srv.InactiveAbandonedUsers(plan_states))
            .Callback(() => order.Enqueue("inactive"))
            .Returns(parallel_gate.Task);
        _serverManagementMoq.Setup(srv => srv.CheckUserServerBalance())
            .Callback(() => order.Enqueue("balance"))
            .Returns(parallel_gate.Task);

        _walletThresholdMoq.Setup(srv => srv.CheckAndApply())
            .Callback(() => order.Enqueue("wallet"))
            .Returns(Task.CompletedTask);

        var job = new JobInterval(App.Services);
        var job_task = job.Execute(Moq.Mock.Of<IJobExecutionContext>());

        // wait for all four parallel jobs to have started
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (order.Count < 5 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        order.Should().HaveCount(5);
        order.Should().StartWith(["update-traffic"]);
        order.Should().Contain(["notif", "deactivate", "inactive", "balance"]);

        // the wallet rule must wait for the parallel block to complete
        _walletThresholdMoq.Verify(srv => srv.CheckAndApply(), Times.Never);

        parallel_gate.SetResult();
        await job_task.WaitAsync(TimeSpan.FromSeconds(5));

        order.Should().HaveCount(6);
        order.Should().EndWith(["wallet"]);

        _serverManagementMoq.Verify(srv => srv.UpdateTrafficData(), Times.Once);
        _serverManagementMoq.Verify(srv => srv.CheckUserServerBalance(), Times.Once);
        _accountMonitoringMoq.Verify(srv => srv.NotifSendServices(plan_states), Times.Once);
        _accountMonitoringMoq.Verify(srv => srv.InactiveAbandonedUsers(plan_states), Times.Once);
        _accountRadiusMoq.Verify(srv => srv.DeactivateInvalidRadiusUsers(plan_states), Times.Once);
        _walletThresholdMoq.Verify(srv => srv.CheckAndApply(), Times.Once);
    }

    [Fact]
    public void JobInterval_IsRegisteredAsNonConcurrentSixtyMinuteQuartzJob()
    {
        typeof(JobInterval).GetCustomAttribute<DisallowConcurrentExecutionAttribute>()
            .Should().NotBeNull("the hourly job must never overlap with itself");

        JobInterval.IntervalInMinutes.Should().Be(60);
    }
}
