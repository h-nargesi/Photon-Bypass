using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Static;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PhotonBypass.Test.Application;

public class ConnectionApplicationTest : ServiceInitializer
{
    public ConnectionApplicationTest()
    {
        var builder = Initialize();

        var priceRepository = new Mock<IRadAcctRepository>();
        priceRepository.Setup(x => x.GetCurrentConnectionList(null))
            .Returns(Task.FromResult(RadAcctData));
        builder.Services.AddSingleton(priceRepository.Object);

        Build(builder);
    }

    static IList<RadAcctEntity> RadAcctData = [
        new RadAcctEntity{

        }
        ];
}
