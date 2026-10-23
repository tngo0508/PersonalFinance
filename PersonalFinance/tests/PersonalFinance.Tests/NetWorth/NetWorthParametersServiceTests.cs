using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.NetWorth;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.Tests.NetWorth;

public class NetWorthParametersServiceTests
{
    private const string UserId = "nw-user";

    [Fact]
    public async Task Save_UpdatesTheTimestampThatCurrentRecordOrdersBy()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NetWorthParameters_{Guid.NewGuid():N}")
            .Options);
        var service = new NetWorthParametersService(db);
        await service.SaveAsync(UserId, NetWorthDefaults.CreateRequest());
        DateTime firstSaved = (await db.NetWorthParameters.SingleAsync()).UpdatedAtUtc;

        await Task.Delay(20);
        NetWorthProjectionRequestDto changed = NetWorthDefaults.CreateRequest();
        changed.AnnualIncome = 120_000m;
        await service.SaveAsync(UserId, changed);

        NetWorthParameters row = await db.NetWorthParameters.AsNoTracking().SingleAsync();
        Assert.True(row.UpdatedAtUtc > firstSaved);
        Assert.Equal(120_000m, (await service.LoadAsync(UserId))!.AnnualIncome);
    }
}
