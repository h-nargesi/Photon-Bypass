using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Infra.Database;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.BasicFunctions.Infrastructure;

public class EventServiceTest : UnitLevelServiceInitializer
{
    private static readonly AccountEntity[] Entities =
    [
        new() { Id = 1 },
        new() { Id = 1 }
    ];
    
    [Fact]
    public async Task OnSave_Register()
    {
        var touch_event = false;

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            event_service.RegisterOnSave<AccountEntity>((_, e) =>
            {
                touch_event = true;
                Assert.NotNull(e);
                Assert.NotNull(e.Entities);
                Assert.Equal(Entities.Length, e.Entities.Length);

                var parameters = e.Entities.Select(a => a.Id).ToHashSet();
                foreach (var entity in Entities)
                {
                    Assert.Contains(entity.Id, parameters);
                }
                
                return Task.CompletedTask;
            });
        }

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            await event_service.CallOnSave(this, new EntityEventArgs<AccountEntity>(Entities));
        }

        Assert.True(touch_event);
    }
    
    [Fact]
    public async Task OnSave_Unregister()
    {
        var touch_event = false;
        var entities = new AccountEntity[]
        {
            new() { Id = 1 },
            new() { Id = 1 },
        };

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            Task FakeDelegate(object? sender, EntityEventArgs<AccountEntity> event_args)
            {
                touch_event = true;
                return Task.CompletedTask;
            }
            
            event_service.RegisterOnSave<AccountEntity>(FakeDelegate);
            event_service.UnregisterOnSave<AccountEntity>(FakeDelegate);
        }

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            await event_service.CallOnSave(this, new EntityEventArgs<AccountEntity>(entities));
        }

        Assert.False(touch_event);
    }
    
    [Fact]
    public async Task OnDelete_Register()
    {
        var touch_event = false;

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            event_service.RegisterOnDelete<AccountEntity>((_, e) =>
            {
                touch_event = true;
                Assert.NotNull(e);
                Assert.NotNull(e.Entities);
                Assert.Equal(Entities.Length, e.Entities.Length);

                var parameters = e.Entities.Select(a => a.Id).ToHashSet();
                foreach (var entity in Entities)
                {
                    Assert.Contains(entity.Id, parameters);
                }
                
                return Task.CompletedTask;
            });
        }

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            await event_service.CallOnDelete(this, new EntityEventArgs<AccountEntity>(Entities));
        }

        Assert.True(touch_event);
    }
    
    [Fact]
    public async Task OnDelete_Unregister()
    {
        var touch_event = false;
        var entities = new AccountEntity[]
        {
            new() { Id = 1 },
            new() { Id = 1 },
        };

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            Task FakeDelegate(object? sender, EntityEventArgs<AccountEntity> event_args)
            {
                touch_event = true;
                return Task.CompletedTask;
            }
            
            event_service.RegisterOnDelete<AccountEntity>(FakeDelegate);
            event_service.UnregisterOnDelete<AccountEntity>(FakeDelegate);
        }

        using (var scope = App.Services.CreateScope())
        {
            var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

            await event_service.CallOnDelete(this, new EntityEventArgs<AccountEntity>(entities));
        }

        Assert.False(touch_event);
    }
}