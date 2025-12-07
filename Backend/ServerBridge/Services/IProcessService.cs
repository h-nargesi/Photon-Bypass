using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Ssh;

namespace PhotonBypass.ServerBridge.Services;

public interface IProcessService
{
    Task<bool> ActivateOn(ProcessEntity process, ServerEntity server, ProcessContext context);

    Task<bool> DeactivateOn(ProcessEntity process, ServerEntity server, ProcessContext context);

    Task<bool> CheckOn(ProcessEntity process, ServerEntity server, ProcessContext context);
}
