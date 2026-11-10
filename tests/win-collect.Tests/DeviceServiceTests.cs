using WinCollect.Services;
using Xunit;

namespace WinCollect.Tests;

public class DeviceServiceTests
{
    [Fact]
    public void LoadOrCreateDevice_ShouldReturnDeviceWithComputerName()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"wincollect-test-{Guid.NewGuid():N}.db");
        DatabaseService.Initialize(tempDb);

        try
        {
            var device = DeviceService.LoadOrCreateDevice("MY-TEST-PC");
            Assert.NotNull(device);
            Assert.Equal("MY-TEST-PC", device.ComputerName);
            Assert.False(string.IsNullOrEmpty(device.DeviceId));
            Assert.True(Guid.TryParse(device.DeviceId, out _));
        }
        finally
        {
            try { File.Delete(tempDb); } catch { }
        }
    }

    [Fact]
    public void DeviceId_ShouldBePersistentAcrossLoads()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"wincollect-test-{Guid.NewGuid():N}.db");
        DatabaseService.Initialize(tempDb);

        try
        {
            var device1 = DeviceService.LoadOrCreateDevice("STABLE-PC");
            // Clear in-memory state and reload
            WinCollect.Infrastructure.AppState.DeviceId = null;
            var device2 = DeviceService.LoadOrCreateDevice("STABLE-PC");

            Assert.Equal(device1.DeviceId, device2.DeviceId);
        }
        finally
        {
            try { File.Delete(tempDb); } catch { }
        }
    }
}
