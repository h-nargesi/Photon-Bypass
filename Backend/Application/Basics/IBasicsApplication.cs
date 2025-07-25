﻿using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Basics;

public interface IBasicsApplication
{
    Task<ApiResult<IList<PriceModel>>> GetPrices();
}
